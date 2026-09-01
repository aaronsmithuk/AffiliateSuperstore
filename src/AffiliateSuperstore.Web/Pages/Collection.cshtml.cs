using System.Text.Json;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using AffiliateSuperstore.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Web.Pages;

public sealed class CollectionModel(
    IShopResolver shopResolver,
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    AnonymousBasketStore basketStore,
    CatalogueSeoPolicy seoPolicy,
    CatalogueSeoOptions seoOptions,
    AffiliateSuperstoreOptions superstoreOptions) : PageModel
{
    public ShopDefinition Shop { get; private set; } = null!;
    public CollectionView Collection { get; private set; } = null!;
    public IReadOnlyList<CollectionLink> Collections { get; private set; } = [];
    public IReadOnlyList<CollectionProductCard> Products { get; private set; } = [];
    public int SavedCount { get; private set; }
    public bool IsIndexable { get; private set; }
    public string CanonicalUrl { get; private set; } = string.Empty;
    public string? StructuredDataJson { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        string shopSlug,
        string collectionSlug,
        CancellationToken cancellationToken)
    {
        var shop = shopResolver.Resolve(Request.Host.Value, Request.Path.Value);
        if (shop is null || !string.Equals(shop.Slug, shopSlug, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        Shop = shop;
        CanonicalUrl = superstoreOptions.BuildPublicUrl($"/{shop.Slug}/{collectionSlug.ToLowerInvariant()}");
        var savedProductIds = basketStore.Get(HttpContext, shop.Slug);
        SavedCount = savedProductIds.Count;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var collection = await context.Collections.AsNoTracking()
            .Where(item => item.Shop.Slug == shop.Slug && item.Slug == collectionSlug && item.IsPublished)
            .Select(item => new CollectionView(
                item.Id,
                item.Slug,
                item.DisplayName,
                item.ShortDescription,
                item.IntroductoryCopy,
                item.SeoTitle,
                item.SeoDescription,
                item.MinimumProductsForIndexing))
            .SingleOrDefaultAsync(cancellationToken);
        if (collection is null) return NotFound();
        Collection = collection;

        Collections = await context.Collections.AsNoTracking()
            .Where(item => item.Shop.Slug == shop.Slug && item.IsPublished)
            .Where(item => item.Products.Any(membership => membership.Product.Shops.Any(shopProduct =>
                shopProduct.ShopId == item.ShopId &&
                shopProduct.IsActive &&
                shopProduct.ReviewStatus == ProductReviewStatus.Approved &&
                shopProduct.Product.IsEligible &&
                shopProduct.Product.AffiliateLinks.Any(link =>
                    link.ShopId == shopProduct.ShopId && link.Status == AffiliateLinkStatus.Active))))
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.DisplayName)
            .Select(item => new CollectionLink(item.Slug, item.DisplayName))
            .ToListAsync(cancellationToken);

        var candidates = await (
            from membership in context.CollectionProducts.AsNoTracking()
            join shopProduct in context.ShopProducts.AsNoTracking()
                on membership.ProductId equals shopProduct.ProductId
            where membership.CollectionId == collection.Id &&
                shopProduct.Shop.Slug == shop.Slug &&
                shopProduct.IsActive &&
                shopProduct.ReviewStatus == ProductReviewStatus.Approved &&
                shopProduct.Product.IsEligible &&
                shopProduct.Product.AffiliateLinks.Any(link =>
                    link.ShopId == shopProduct.ShopId && link.Status == AffiliateLinkStatus.Active)
            orderby membership.IsFeatured descending,
                membership.DisplayOrder,
                shopProduct.IsFeatured descending,
                shopProduct.DisplayOrder,
                shopProduct.Product.Snapshots.Max(snapshot => snapshot.RecentSalesVolume) descending
            select new CollectionProductCard(
                shopProduct.ProductId,
                shopProduct.EditorialTitle ?? shopProduct.Product.Title,
                shopProduct.EditorialTitle,
                shopProduct.EditorialDescription,
                shopProduct.Product.MainImageUrl,
                shopProduct.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc)
                    .Select(snapshot => snapshot.SalePrice).FirstOrDefault(),
                shopProduct.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc)
                    .Select(snapshot => snapshot.Currency).FirstOrDefault(),
                shopProduct.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc)
                    .Select(snapshot => snapshot.EvaluationRate).FirstOrDefault(),
                shopProduct.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc)
                    .Select(snapshot => snapshot.RecentSalesVolume).FirstOrDefault(),
                shopProduct.Product.SecondLevelCategoryName,
                shopProduct.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc)
                    .Select(snapshot => snapshot.FetchedUtc).FirstOrDefault(),
                false))
            .Take(48)
            .ToListAsync(cancellationToken);

        Products = candidates.Select(product => product with
        {
            IsSaved = savedProductIds.Contains(product.ProductId, StringComparer.Ordinal)
        }).ToArray();
        var indexableCount = candidates.Count(candidate => seoPolicy.IsProductIndexable(
            candidate.EditorialTitle,
            candidate.EditorialDescription,
            candidate.ImageUrl,
            candidate.Price,
            candidate.LastCheckedUtc));
        IsIndexable = seoOptions.IndexingEnabled && indexableCount >= collection.MinimumProductsForIndexing;

        StructuredDataJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "CollectionPage",
            ["name"] = collection.DisplayName,
            ["description"] = collection.SeoDescription,
            ["url"] = CanonicalUrl,
            ["mainEntity"] = new Dictionary<string, object?>
            {
                ["@type"] = "ItemList",
                ["numberOfItems"] = Products.Count,
                ["itemListElement"] = Products.Select((product, index) => new Dictionary<string, object?>
                {
                    ["@type"] = "ListItem",
                    ["position"] = index + 1,
                    ["url"] = superstoreOptions.BuildPublicUrl($"/{shop.Slug}/product/{product.ProductId}"),
                    ["name"] = product.Title
                }).ToArray()
            }
        });
        return Page();
    }

    public string FormatPrice(decimal? price, string? currency)
    {
        if (price is null) return "See current price";
        return string.Equals(currency, "GBP", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(currency)
            ? $"From £{price.Value:N2}"
            : $"From {currency} {price.Value:N2}";
    }

    public sealed record CollectionView(
        Guid Id,
        string Slug,
        string DisplayName,
        string ShortDescription,
        string IntroductoryCopy,
        string SeoTitle,
        string SeoDescription,
        int MinimumProductsForIndexing);

    public sealed record CollectionLink(string Slug, string DisplayName);

    public sealed record CollectionProductCard(
        string ProductId,
        string Title,
        string? EditorialTitle,
        string? EditorialDescription,
        string? ImageUrl,
        decimal? Price,
        string? Currency,
        decimal? EvaluationRate,
        long? RecentSalesVolume,
        string? Category,
        DateTimeOffset LastCheckedUtc,
        bool IsSaved);
}
