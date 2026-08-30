using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using AffiliateSuperstore.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Web.Pages;

public sealed class ProductModel(
    AffiliateSuperstoreOptions superstoreOptions,
    AnonymousBasketStore basketStore,
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory) : PageModel
{
    public ShopDefinition Shop { get; private set; } = null!;
    public ProductView Product { get; private set; } = null!;
    public int SavedCount { get; private set; }

    public async Task<IActionResult> OnGetAsync(string shopSlug, string productId, CancellationToken cancellationToken)
    {
        var shop = superstoreOptions.Shops.SingleOrDefault(item =>
            item.IsEnabled && string.Equals(item.Slug, shopSlug, StringComparison.OrdinalIgnoreCase));
        if (shop is null) return NotFound();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var product = await context.ShopProducts.AsNoTracking()
            .Where(item =>
                item.Shop.Slug == shop.Slug &&
                item.ProductId == productId &&
                item.Shop.IsEnabled &&
                item.IsActive &&
                item.ReviewStatus == ProductReviewStatus.Approved &&
                item.Product.IsEligible &&
                item.Product.AffiliateLinks.Any(link => link.ShopId == item.ShopId && link.Status == AffiliateLinkStatus.Active))
            .Select(item => new ProductView(
                item.ProductId,
                item.EditorialTitle ?? item.Product.Title,
                item.EditorialDescription,
                item.Product.MainImageUrl,
                item.Product.FirstLevelCategoryName,
                item.Product.SecondLevelCategoryName,
                item.Product.SellerName,
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.SalePrice).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.OriginalPrice).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.Currency).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.DiscountText).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.EvaluationRate).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.RecentSalesVolume).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.FetchedUtc).FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null) return NotFound();

        Shop = shop;
        Product = product;
        SavedCount = basketStore.Get(HttpContext, shop.Slug).Count;
        return Page();
    }

    public sealed record ProductView(
        string ProductId,
        string Title,
        string? EditorialDescription,
        string? ImageUrl,
        string? FirstCategory,
        string? SecondCategory,
        string? SellerName,
        decimal? SalePrice,
        decimal? OriginalPrice,
        string? Currency,
        string? Discount,
        decimal? EvaluationRate,
        long? RecentSalesVolume,
        DateTimeOffset LastCheckedUtc);
}
