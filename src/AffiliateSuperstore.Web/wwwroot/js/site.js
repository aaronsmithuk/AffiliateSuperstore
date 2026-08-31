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
