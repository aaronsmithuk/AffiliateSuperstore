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
    public IReadOnlyList<RelatedProduct> RelatedProducts { get; private set; } = [];
    public int SavedCount { get; private set; }
    public bool IsSaved { get; private set; }
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
                item.Product.SkuId,
                item.Product.EanCode,
                item.Product.LastDetailRefreshedUtc,
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.SalePrice).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.OriginalPrice).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.Currency).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.DiscountText).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.EvaluationRate).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.RecentSalesVolume).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.FetchedUtc).FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null) return NotFound();

        var media = await context.ProductMedia.AsNoTracking()
            .Where(item => item.ProductId == product.ProductId)
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Position)
            .Select(item => new ProductMediaView(item.Type, item.Url, item.Position))
            .ToListAsync(cancellationToken);

        Shop = shop;
        Product = product with { Media = media };
        CanonicalUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/{shop.Slug}/product/{product.ProductId}";
        IsIndexable = seoOptions.IndexingEnabled && seoPolicy.IsProductIndexable(
            product.EditorialTitle,
            product.EditorialDescription,
            product.ImageUrl,
            product.SalePrice,
            product.LastCheckedUtc);
        StructuredDataJson = BuildStructuredData(Product);
        var savedProductIds = basketStore.Get(HttpContext, shop.Slug);
        SavedCount = savedProductIds.Count;
        IsSaved = savedProductIds.Contains(product.ProductId, StringComparer.Ordinal);
        RelatedProducts = await context.ShopProducts.AsNoTracking()
            .Where(item =>
                item.Shop.Slug == shop.Slug &&
                item.ProductId != product.ProductId &&
                item.Shop.IsEnabled &&
                item.IsActive &&
                item.ReviewStatus == ProductReviewStatus.Approved &&
                item.Product.IsEligible &&
                item.Product.SecondLevelCategoryName == product.SecondCategory &&
                item.Product.AffiliateLinks.Any(link => item.ShopId == link.ShopId && link.Status == AffiliateLinkStatus.Active))
            .OrderByDescending(item => item.IsFeatured)
            .ThenByDescending(item => item.Product.Snapshots.Max(snapshot => snapshot.RecentSalesVolume))
            .Take(4)
            .Select(item => new RelatedProduct(
                item.ProductId,
                item.EditorialTitle ?? item.Product.Title,
                item.Product.MainImageUrl,
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.SalePrice).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.Currency).FirstOrDefault()))
            .ToListAsync(cancellationToken);
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
            ["image"] = product.ImageUrls.Count == 0 ? null : product.ImageUrls,
            ["sku"] = product.SkuId ?? product.ProductId,
            ["gtin13"] = string.IsNullOrWhiteSpace(product.EanCode) ? null : product.EanCode,
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
        string? SkuId,
        string? EanCode,
        DateTimeOffset? LastDetailRefreshedUtc,
        decimal? SalePrice,
        decimal? OriginalPrice,
        string? Currency,
        string? Discount,
        decimal? EvaluationRate,
        long? RecentSalesVolume,
        DateTimeOffset LastCheckedUtc)
    {
        public string Title => EditorialTitle ?? SourceTitle;
        public IReadOnlyList<ProductMediaView> Media { get; init; } = [];
        public IReadOnlyList<string> ImageUrls => Media
            .Where(item => item.Type == ProductMediaType.Image)
            .Select(item => item.Url)
            .Prepend(ImageUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        public IReadOnlyList<string> VideoUrls => Media
            .Where(item => item.Type == ProductMediaType.Video)
            .Select(item => item.Url)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public sealed record ProductMediaView(ProductMediaType Type, string Url, int Position);

    public sealed record RelatedProduct(
        string ProductId,
        string Title,
        string? ImageUrl,
        decimal? Price,
        string? Currency);
}
