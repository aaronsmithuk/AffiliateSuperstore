using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Web.Pages;

public sealed class ShopModel(
    IShopResolver shopResolver,
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory) : PageModel
{
    public ShopDefinition Shop { get; private set; } = null!;
    public IReadOnlyList<ShopProductCard> Products { get; private set; } = [];
    public string? Query { get; private set; }

    public async Task<IActionResult> OnGetAsync(string shopSlug, string? q, CancellationToken cancellationToken)
    {
        var shop = shopResolver.Resolve(Request.Host.Value, Request.Path.Value);
        if (shop is null || !string.Equals(shop.Slug, shopSlug, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        Shop = shop;
        Query = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
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
        if (Query is not null)
        {
            query = query.Where(item =>
                item.Product.Title.Contains(Query) ||
                (item.EditorialTitle != null && item.EditorialTitle.Contains(Query)));
        }

        Products = await query
            .OrderByDescending(item => item.IsFeatured)
            .ThenBy(item => item.DisplayOrder)
            .ThenByDescending(item => item.Product.Snapshots.Max(snapshot => snapshot.RecentSalesVolume))
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
