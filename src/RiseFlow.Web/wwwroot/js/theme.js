window.riseFlowTheme = (function () {
    const key = "riseflow-theme";

    function apply(theme) {
        const root = document.documentElement;
        if (theme === "dark") {
            root.classList.add("dark");
        } else {
            root.classList.remove("dark");
        }
    }

    function getPreferred() {
        const stored = localStorage.getItem(key);
        if (stored === "light" || stored === "dark") return stored;
        const prefersDark = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
        return prefersDark ? "dark" : "light";
    }

    return {
        init: function () {
            const theme = getPreferred();
            apply(theme);
            return theme === "dark";
        },
        toggle: function () {
            const current = getPreferred();
            const next = current === "dark" ? "light" : "dark";
            localStorage.setItem(key, next);
            apply(next);
            return next === "dark";
        }
    };
})();

