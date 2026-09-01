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
