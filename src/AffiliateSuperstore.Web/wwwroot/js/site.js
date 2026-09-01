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
    const page = document.querySelector("[data-analytics-page]");
    let enabled = false;
    let pageEventSent = false;

    const numberValue = (value) => {
        if (value === undefined || value === null || value === "") return undefined;
        const parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : undefined;
    };

    const compact = (value) => Object.fromEntries(Object.entries(value)
        .filter(([, item]) => item !== undefined && item !== null && item !== ""));

    const itemFrom = (element, index) => compact({
        item_id: element.dataset.analyticsItemId,
        item_category: element.dataset.analyticsItemCategory,
        item_list_id: element.dataset.analyticsShop,
        item_list_name: element.dataset.analyticsPlacement,
        index,
        price: numberValue(element.dataset.analyticsItemPrice),
        quantity: 1
    });

    const itemsWithin = (container) => [...container.querySelectorAll("[data-analytics-item]")]
        .slice(0, 50)
        .map((element, index) => itemFrom(element, index));

    const addValue = (parameters, items) => {
        const prices = items.map((item) => item.price).filter((price) => price !== undefined);
        const currency = page?.dataset.analyticsCurrency;
        return compact({
            ...parameters,
            currency: prices.length > 0 ? currency : undefined,
            value: prices.length > 0 ? prices.reduce((total, price) => total + price, 0) : undefined,
            items
        });
    };

    const buildPageEvent = () => {
        if (!page) return undefined;

        const shop = page.dataset.analyticsShop;
        const pageType = page.dataset.analyticsPage;
        if (pageType === "catalogue" || pageType === "collection") {
            const items = itemsWithin(page);
            const collection = page.dataset.analyticsCollection;
            return {
                name: "view_item_list",
                parameters: addValue({
                    item_list_id: collection ? `${shop}:${collection}` : shop,
                    item_list_name: collection || "shop_catalogue",
                    shop,
                    collection,
                    result_count: numberValue(page.dataset.analyticsResultCount),
                    search_used: page.dataset.analyticsSearchUsed === "true",
                    category_filter_used: page.dataset.analyticsCategoryUsed === "true",
                    price_filter_used: page.dataset.analyticsPriceUsed === "true",
                    sort: page.dataset.analyticsSort
                }, items)
            };
        }

        if (pageType === "product") {
            const item = itemFrom(page, 0);
            return {
                name: "view_item",
                parameters: addValue({ shop }, [item])
            };
        }

        if (pageType === "saved_list") {
            const items = itemsWithin(page);
            return {
                name: "view_saved_list",
                parameters: addValue({
                    shop,
                    item_count: items.length
                }, items)
            };
        }

        return undefined;
    };

    const pageEvent = buildPageEvent();
    const track = (name, parameters = {}) => {
        if (!enabled || typeof window.gtag !== "function") return false;
        window.gtag("event", name, compact(parameters));
        return true;
    };

    const enable = () => {
        enabled = true;
        if (pageEvent && !pageEventSent) {
            pageEventSent = track(pageEvent.name, pageEvent.parameters);
        }
    };

    const interactionParameters = (element) => addValue({
        shop: element.dataset.analyticsShop,
        placement: element.dataset.analyticsPlacement
    }, [itemFrom(element, 0)]);

    document.addEventListener("click", (event) => {
        if (!(event.target instanceof Element)) return;

        const selection = event.target.closest("[data-analytics-select-item]");
        if (selection) {
            track("select_item", interactionParameters(selection));
            return;
        }

        const handoff = event.target.closest("[data-analytics-affiliate-click]");
        if (handoff) {
            track("affiliate_handoff", {
                ...interactionParameters(handoff),
                transport_type: "beacon"
            });
        }
    });

    document.addEventListener("submit", (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;

        if (form.matches("[data-analytics-save-item]")) {
            track("add_to_wishlist", interactionParameters(form));
        } else if (form.matches("[data-analytics-remove-item]")) {
            track("remove_from_wishlist", interactionParameters(form));
        } else if (form.matches("[data-analytics-clear-list]")) {
            track("clear_saved_list", {
                shop: form.dataset.analyticsShop,
                item_count: numberValue(form.dataset.analyticsItemCount)
            });
        } else if (form.matches("[data-analytics-search]")) {
            const query = form.querySelector('input[name="q"]');
            track("catalogue_search_submit", {
                shop: form.dataset.analyticsShop,
                search_used: query instanceof HTMLInputElement && query.value.trim().length > 0
            });
        } else if (form.matches("[data-analytics-filter]")) {
            const category = form.querySelector('select[name="category"]');
            const minimumPrice = form.querySelector('input[name="minPrice"]');
            const maximumPrice = form.querySelector('input[name="maxPrice"]');
            const sort = form.querySelector('select[name="sort"]');
            track("catalogue_filter_submit", {
                shop: form.dataset.analyticsShop,
                category_filter_used: category instanceof HTMLSelectElement && category.value.length > 0,
                price_filter_used:
                    (minimumPrice instanceof HTMLInputElement && minimumPrice.value.length > 0) ||
                    (maximumPrice instanceof HTMLInputElement && maximumPrice.value.length > 0),
                sort: sort instanceof HTMLSelectElement ? sort.value : undefined
            });
        }
    });

    window.wonderAisleAnalytics = Object.freeze({ enable, track });
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
            allow_ad_personalization_signals: false,
            page_location: document.querySelector('link[rel="canonical"]')?.href ||
                `${window.location.origin}${window.location.pathname}`
        });

        window.wonderAisleAnalytics?.enable();

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
