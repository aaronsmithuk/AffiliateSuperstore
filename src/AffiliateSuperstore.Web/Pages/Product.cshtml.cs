using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using AffiliateSuperstore.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AffiliateSuperstore.Web.Pages;

public sealed class ProductModel(
    AffiliateSuperstoreOptions superstoreOptions,
    AnonymousBasketStore basketStore,
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    CatalogueSeoPolicy seoPolicy,
    CatalogueSeoOptions seoOptions) : PageModel
{
    public ShopDefinition Shop { get; private set; } = null!;
    public ProductView Product { get; private set; } = null!;
    public int SavedCount { get; private set; }
    public bool IsIndexable { get; private set; }
    public string CanonicalUrl { get; private set; } = string.Empty;
    public string? StructuredDataJson { get; private set; }

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
                item.Product.Title,
                item.EditorialTitle,
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
        CanonicalUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/{shop.Slug}/product/{product.ProductId}";
        IsIndexable = seoOptions.IndexingEnabled && seoPolicy.IsProductIndexable(
            product.EditorialTitle,
            product.EditorialDescription,
            product.ImageUrl,
            product.SalePrice,
            product.LastCheckedUtc);
        StructuredDataJson = BuildStructuredData(product);
        SavedCount = basketStore.Get(HttpContext, shop.Slug).Count;
        return Page();
    }

    private string BuildStructuredData(ProductView product)
    {
        var data = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = product.Title,
            ["description"] = product.EditorialDescription,
            ["image"] = string.IsNullOrWhiteSpace(product.ImageUrl) ? null : new[] { product.ImageUrl },
            ["sku"] = product.ProductId,
            ["category"] = product.SecondCategory ?? product.FirstCategory,
            ["url"] = CanonicalUrl
        };
        if (product.SalePrice is > 0)
        {
            data["offers"] = new Dictionary<string, object?>
            {
                ["@type"] = "Offer",
                ["url"] = CanonicalUrl,
                ["priceCurrency"] = product.Currency ?? "GBP",
                ["price"] = product.SalePrice.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                ["seller"] = string.IsNullOrWhiteSpace(product.SellerName) ? null : new Dictionary<string, object?>
                {
                    ["@type"] = "Organization",
                    ["name"] = product.SellerName
                }
            };
        }
        return JsonSerializer.Serialize(data);
    }

    public sealed record ProductView(
        string ProductId,
        string SourceTitle,
        string? EditorialTitle,
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
        DateTimeOffset LastCheckedUtc)
    {
        public string Title => EditorialTitle ?? SourceTitle;
    }
}
