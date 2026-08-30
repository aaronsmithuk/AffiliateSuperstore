using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using AffiliateSuperstore.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Web.Pages;

public sealed class BasketModel(
    AffiliateSuperstoreOptions superstoreOptions,
    AnonymousBasketStore basketStore,
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory) : PageModel
{
    public ShopDefinition Shop { get; private set; } = null!;
    public IReadOnlyList<BasketProduct> Products { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string shopSlug, CancellationToken cancellationToken)
    {
        var shop = FindShop(shopSlug);
        if (shop is null) return NotFound();
        Shop = shop;

        var productIds = basketStore.Get(HttpContext, shopSlug);
        if (productIds.Count == 0) return Page();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var products = await context.ShopProducts.AsNoTracking()
            .Where(item =>
                item.Shop.Slug == shopSlug &&
                productIds.Contains(item.ProductId))
            .Select(item => new BasketProduct(
                item.ProductId,
                item.EditorialTitle ?? item.Product.Title,
                item.Product.MainImageUrl,
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.SalePrice).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.Currency).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.EvaluationRate).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.RecentSalesVolume).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => (DateTimeOffset?)snapshot.FetchedUtc).FirstOrDefault(),
                item.Shop.IsEnabled &&
                item.IsActive &&
                item.ReviewStatus == ProductReviewStatus.Approved &&
                item.Product.IsEligible &&
                item.Product.AffiliateLinks.Any(link => link.ShopId == item.ShopId && link.Status == AffiliateLinkStatus.Active)))
            .ToListAsync(cancellationToken);
        var productsById = products.ToDictionary(product => product.ProductId, StringComparer.Ordinal);
        Products = productIds
            .Select(productId => productsById.GetValueOrDefault(productId) ?? new BasketProduct(
                productId,
                "Saved product",
                null,
                null,
                null,
                null,
                null,
                null,
                false))
            .ToArray();
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync(string shopSlug, string productId, CancellationToken cancellationToken)
    {
        if (FindShop(shopSlug) is null) return NotFound();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var canSave = await context.ShopProducts.AsNoTracking().AnyAsync(item =>
            item.Shop.Slug == shopSlug &&
            item.ProductId == productId &&
            item.Shop.IsEnabled &&
            item.IsActive &&
            item.ReviewStatus == ProductReviewStatus.Approved &&
            item.Product.IsEligible,
            cancellationToken);
        if (!canSave) return NotFound();
        basketStore.Add(HttpContext, shopSlug, productId);
        return RedirectToPage("/Basket", new { shopSlug });
    }

    public IActionResult OnPostRemove(string shopSlug, string productId)
    {
        if (FindShop(shopSlug) is null) return NotFound();
        basketStore.Remove(HttpContext, shopSlug, productId);
        return RedirectToPage("/Basket", new { shopSlug });
    }

    public IActionResult OnPostClear(string shopSlug)
    {
        if (FindShop(shopSlug) is null) return NotFound();
        basketStore.Clear(HttpContext, shopSlug);
        return RedirectToPage("/Basket", new { shopSlug });
    }

    private ShopDefinition? FindShop(string shopSlug) => superstoreOptions.Shops.SingleOrDefault(
        shop => shop.IsEnabled && string.Equals(shop.Slug, shopSlug, StringComparison.OrdinalIgnoreCase));

    public sealed record BasketProduct(
        string ProductId,
        string Title,
        string? ImageUrl,
        decimal? Price,
        string? Currency,
        decimal? EvaluationRate,
        long? RecentSalesVolume,
        DateTimeOffset? LastCheckedUtc,
        bool IsAvailable);
}
