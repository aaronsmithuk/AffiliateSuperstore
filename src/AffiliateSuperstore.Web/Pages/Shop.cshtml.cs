using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using AffiliateSuperstore.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Web.Pages;

public sealed class ShopModel(
    IShopResolver shopResolver,
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    AnonymousBasketStore basketStore) : PageModel
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
        SavedCount = basketStore.Get(HttpContext, shop.Slug).Count;
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

        query = Sort switch
        {
            "price-asc" => query.OrderBy(item => item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.SalePrice).FirstOrDefault()),
            "price-desc" => query.OrderByDescending(item => item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.SalePrice).FirstOrDefault()),
            "newest" => query.OrderByDescending(item => item.Product.LastRefreshedUtc),
            _ => query.OrderByDescending(item => item.IsFeatured)
                .ThenBy(item => item.DisplayOrder)
                .ThenByDescending(item => item.Product.Snapshots.Max(snapshot => snapshot.RecentSalesVolume))
        };

        Products = await query
            .Take(48)
            .Select(item => new ShopProductCard(
                item.ProductId,
                item.EditorialTitle ?? item.Product.Title,
                item.Product.MainImageUrl,
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.SalePrice).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.Currency).FirstOrDefault()))
            .ToListAsync(cancellationToken);
        return Page();
    }

    public sealed record ShopProductCard(string ProductId, string Title, string? ImageUrl, decimal? Price, string? Currency);
}
