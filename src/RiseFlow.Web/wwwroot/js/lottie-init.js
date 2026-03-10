window.riseFlowLottie = (function () {
    function initHero() {
        const el = document.getElementById("lottie-how");
        if (!el || !window.lottie) return;

        // Expecting a Lottie JSON file under wwwroot/media; adjust name as needed.
        window.lottie.loadAnimation({
            container: el,
            renderer: "svg",
            loop: true,
            autoplay: true,
            path: "/media/how-schools-get-started.json"
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        initHero();
    });

    return {
        initHero
    };
})();

