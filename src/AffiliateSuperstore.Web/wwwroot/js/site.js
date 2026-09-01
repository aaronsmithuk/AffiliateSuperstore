// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.querySelectorAll("[data-product-gallery]").forEach((gallery) => {
    const media = gallery.closest(".product-detail-media");
    const mainImage = media?.querySelector("[data-product-main-image]");
    const choices = [...gallery.querySelectorAll("[data-gallery-image]")];
    if (!mainImage || choices.length === 0) return;

    choices.forEach((choice) => {
        choice.addEventListener("click", () => {
            const imageUrl = choice.dataset.galleryImage;
            if (!imageUrl) return;

            mainImage.src = imageUrl;
            mainImage.alt = choice.dataset.galleryAlt || mainImage.alt;
            choices.forEach((item) => item.setAttribute("aria-pressed", String(item === choice)));
        });
    });
});

(() => {
    const candidates = [...document.querySelectorAll("[data-affiliate-impression]")];
    const token = document.querySelector('meta[name="request-verification-token"]')?.content;
    if (candidates.length === 0 || !token || !("IntersectionObserver" in window)) return;

    const recorded = new Set();
    let pending = [];
    let flushTimer;

    const flush = () => {
        clearTimeout(flushTimer);
        flushTimer = undefined;
        if (pending.length === 0) return;

        const items = pending;
        pending = [];
        fetch("/analytics/impressions", {
            method: "POST",
            credentials: "same-origin",
            keepalive: true,
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": token
            },
            body: JSON.stringify({ items })
        }).catch(() => {
            // Measurement must never interrupt the storefront experience.
        });
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (!entry.isIntersecting || entry.intersectionRatio < 0.25) return;

            const element = entry.target;
            const shop = element.dataset.impressionShop;
            const productId = element.dataset.impressionProduct;
            const placement = element.dataset.impressionPlacement;
            const key = `${shop}|${productId}|${placement}`;
            observer.unobserve(element);
            if (!shop || !productId || !placement || recorded.has(key)) return;

            recorded.add(key);
            pending.push({ shop, productId, placement });
            if (pending.length >= 16) flush();
            else if (!flushTimer) flushTimer = window.setTimeout(flush, 400);
        });
    }, { threshold: 0.25 });

    candidates.forEach((candidate) => observer.observe(candidate));
    window.addEventListener("pagehide", flush);
})();

(() => {
    const banner = document.querySelector("[data-cookie-consent]");
    if (!banner || typeof window.gtag !== "function") return;

    const measurementId = banner.dataset.measurementId;
    const preferenceButton = document.querySelector("[data-cookie-settings]");
    const choiceButtons = [...banner.querySelectorAll("[data-cookie-consent-choice]")];
    const cookieName = "wonderaisle_analytics_consent";
    const maxAgeSeconds = 60 * 60 * 24 * 180;
    let analyticsLoaded = false;

    const readChoice = () => document.cookie
        .split(";")
        .map((part) => part.trim())
        .find((part) => part.startsWith(`${cookieName}=`))
        ?.split("=")[1];

    const writeChoice = (choice) => {
        const secure = window.location.protocol === "https:" ? "; Secure" : "";
        document.cookie = `${cookieName}=${choice}; Path=/; Max-Age=${maxAgeSeconds}; SameSite=Lax${secure}`;
    };

    const loadAnalytics = () => {
        if (analyticsLoaded || !measurementId) return;
        analyticsLoaded = true;

        window.gtag("consent", "update", {
            analytics_storage: "granted",
            ad_storage: "denied",
            ad_user_data: "denied",
            ad_personalization: "denied"
        });
        window.gtag("js", new Date());
        window.gtag("config", measurementId, {
            cookie_expires: maxAgeSeconds,
            cookie_update: false,
            allow_google_signals: false,
            allow_ad_personalization_signals: false
        });

        const script = document.createElement("script");
        script.async = true;
        script.src = `https://www.googletagmanager.com/gtag/js?id=${encodeURIComponent(measurementId)}`;
        document.head.appendChild(script);
    };

    const rejectAnalytics = () => {
        window.gtag("consent", "update", {
            analytics_storage: "denied",
            ad_storage: "denied",
            ad_user_data: "denied",
            ad_personalization: "denied"
        });

        const secure = window.location.protocol === "https:" ? "; Secure" : "";
        const cookieNames = ["_ga", `_ga_${measurementId.replace(/^G-/, "")}`];
        const registrableHost = window.location.hostname.replace(/^www\./, "");
        cookieNames.forEach((name) => {
            document.cookie = `${name}=; Path=/; Max-Age=0; SameSite=Lax${secure}`;
            if (registrableHost.includes(".")) {
                document.cookie = `${name}=; Path=/; Domain=.${registrableHost}; Max-Age=0; SameSite=Lax${secure}`;
            }
        });
    };

    const applyChoice = (choice, persist) => {
        if (persist) writeChoice(choice);

        if (choice === "accepted") loadAnalytics();
        else rejectAnalytics();

        banner.hidden = true;
    };

    choiceButtons.forEach((button) => {
        button.addEventListener("click", () => {
            const choice = button.dataset.cookieConsentChoice;
            const reloadAfterRejecting = choice === "rejected" && analyticsLoaded;
            applyChoice(choice, true);
            if (reloadAfterRejecting) window.location.reload();
            else preferenceButton?.focus();
        });
    });

    preferenceButton?.addEventListener("click", () => {
        banner.hidden = false;
        choiceButtons[0]?.focus();
    });

    const savedChoice = readChoice();
    if (savedChoice === "accepted" || savedChoice === "rejected") {
        applyChoice(savedChoice, false);
    } else {
        banner.hidden = false;
    }
})();
