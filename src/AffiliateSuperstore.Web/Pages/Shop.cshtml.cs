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

public sealed class ShopModel(
    IShopResolver shopResolver,
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    AnonymousBasketStore basketStore,
    CatalogueSeoPolicy seoPolicy,
    CatalogueSeoOptions seoOptions,
    AffiliateSuperstoreOptions superstoreOptions) : PageModel
{
    public ShopDefinition Shop { get; private set; } = null!;
    public IReadOnlyList<ShopProductCard> Products { get; private set; } = [];
    public string? Query { get; private set; }
    public decimal? MinimumPrice { get; private set; }
    public decimal? MaximumPrice { get; private set; }
    public string Sort { get; private set; } = "popular";
    public string? Category { get; private set; }
    public IReadOnlyList<string> Categories { get; private set; } = [];
    public int SavedCount { get; private set; }
    public int ResultCount { get; private set; }
    public bool IsIndexable { get; private set; }
    public bool HasActiveFilters { get; private set; }
    public string CanonicalUrl { get; private set; } = string.Empty;
    public string? StructuredDataJson { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        string shopSlug,
        string? q,
        decimal? minPrice,
        decimal? maxPrice,
        string? category,
        string? sort,
        CancellationToken cancellationToken)
    {
        var shop = shopResolver.Resolve(Request.Host.Value, Request.Path.Value);
        if (shop is null || !string.Equals(shop.Slug, shopSlug, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        Shop = shop;
        Query = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        MinimumPrice = minPrice is >= 0 and <= 10000 ? minPrice : null;
        MaximumPrice = maxPrice is >= 0 and <= 10000 ? maxPrice : null;
        if (MinimumPrice > MaximumPrice) (MinimumPrice, MaximumPrice) = (MaximumPrice, MinimumPrice);
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        Sort = sort is "price-asc" or "price-desc" or "newest" ? sort : "popular";
        HasActiveFilters = Query is not null || Category is not null || MinimumPrice is not null || MaximumPrice is not null || Sort != "popular";
        CanonicalUrl = superstoreOptions.BuildPublicUrl(shop.PathPrefix);
        var savedProductIds = basketStore.Get(HttpContext, shop.Slug);
        SavedCount = savedProductIds.Count;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.ShopProducts.AsNoTracking()
            .Where(item =>
                item.Shop.Slug == shop.Slug &&
                item.Shop.IsEnabled &&
                item.IsActive &&
                item.ReviewStatus == ProductReviewStatus.Approved &&
                item.Product.IsEligible &&
                item.Product.AffiliateLinks.Any(link =>
                    link.ShopId == item.ShopId && link.Status == AffiliateLinkStatus.Active));
        Categories = await query
            .Where(item => item.Product.SecondLevelCategoryName != null)
            .Select(item => item.Product.SecondLevelCategoryName!)
            .Distinct()
            .OrderBy(value => value)
            .ToListAsync(cancellationToken);
        var seoCandidates = await query
            .Select(item => new
            {
                item.EditorialTitle,
                item.EditorialDescription,
                item.Product.MainImageUrl,
                Price = item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.SalePrice).FirstOrDefault(),
                LastCheckedUtc = item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.FetchedUtc).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        IsIndexable = seoOptions.IndexingEnabled && CatalogueSeoPolicy.IsShopIndexable(seoCandidates.Count(candidate =>
            seoPolicy.IsProductIndexable(
                candidate.EditorialTitle,
                candidate.EditorialDescription,
                candidate.MainImageUrl,
                candidate.Price,
                candidate.LastCheckedUtc)));
        if (Query is not null)
        {
            query = query.Where(item =>
                item.Product.Title.Contains(Query) ||
                (item.EditorialTitle != null && item.EditorialTitle.Contains(Query)));
        }
        if (Category is not null) query = query.Where(item => item.Product.SecondLevelCategoryName == Category);
        if (MinimumPrice is not null)
        {
            query = query.Where(item => item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.SalePrice).FirstOrDefault() >= MinimumPrice);
        }
        if (MaximumPrice is not null)
        {
            query = query.Where(item => item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.SalePrice).FirstOrDefault() <= MaximumPrice);
        }

        ResultCount = await query.CountAsync(cancellationToken);

        query = Sort switch
        {
            "price-asc" => query.OrderBy(item => item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.SalePrice).FirstOrDefault()),
            "price-desc" => query.OrderByDescending(item => item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.SalePrice).FirstOrDefault()),
            "newest" => query.OrderByDescending(item => item.Product.LastRefreshedUtc),
            _ => query.OrderByDescending(item => item.IsFeatured)
                .ThenBy(item => item.DisplayOrder)
                .ThenByDescending(item => item.Product.Snapshots.Max(snapshot => snapshot.RecentSalesVolume))
        };

        var products = await query
            .Take(48)
            .Select(item => new ShopProductCard(
                item.ProductId,
                item.EditorialTitle ?? item.Product.Title,
                item.Product.MainImageUrl,
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.SalePrice).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.Currency).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.EvaluationRate).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.RecentSalesVolume).FirstOrDefault(),
                item.Product.SellerName,
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.FetchedUtc).FirstOrDefault(),
                false))
            .ToListAsync(cancellationToken);
        Products = products
            .Select(product => product with { IsSaved = savedProductIds.Contains(product.ProductId, StringComparer.Ordinal) })
            .ToArray();
        if (!HasActiveFilters && Products.Count > 0)
        {
            StructuredDataJson = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "ItemList",
                ["name"] = Shop.SeoTitle,
                ["description"] = Shop.SeoDescription,
                ["url"] = CanonicalUrl,
                ["numberOfItems"] = Products.Count,
                ["itemListElement"] = Products.Select((product, index) => new Dictionary<string, object?>
                {
                    ["@type"] = "ListItem",
                    ["position"] = index + 1,
                    ["url"] = superstoreOptions.BuildPublicUrl($"/{Shop.Slug}/product/{product.ProductId}"),
                    ["name"] = product.Title
                }).ToArray()
            });
        }
        return Page();
    }

    public sealed record ShopProductCard(
        string ProductId,
        string Title,
        string? ImageUrl,
        decimal? Price,
        string? Currency,
        decimal? EvaluationRate,
        long? RecentSalesVolume,
        string? SellerName,
        DateTimeOffset LastCheckedUtc,
        bool IsSaved);
}
