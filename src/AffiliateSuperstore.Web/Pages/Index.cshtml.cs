using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Web.Pages;

public sealed class IndexModel(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    AffiliateSuperstoreOptions superstoreOptions) : PageModel
{
    public IReadOnlyList<HomeShopEntry> Shops { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var enabledShops = superstoreOptions.Shops
            .Where(shop => shop.IsEnabled)
            .OrderBy(shop => shop.DisplayName)
            .ToArray();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entries = new List<HomeShopEntry>(enabledShops.Length);

        for (var index = 0; index < enabledShops.Length; index++)
        {
            var shop = enabledShops[index];
            var previewImages = await context.ShopProducts
                .AsNoTracking()
                .Where(item =>
                    item.Shop.Slug == shop.Slug &&
                    item.Shop.IsEnabled &&
                    item.IsActive &&
                    item.ReviewStatus == ProductReviewStatus.Approved &&
                    item.Product.IsEligible &&
                    item.Product.MainImageUrl != null &&
                    item.Product.AffiliateLinks.Any(link =>
                        link.ShopId == item.ShopId && link.Status == AffiliateLinkStatus.Active))
                .OrderByDescending(item => item.IsFeatured)
                .ThenBy(item => item.DisplayOrder)
                .Select(item => item.Product.MainImageUrl!)
                .Take(6)
                .ToListAsync(cancellationToken);

            entries.Add(new HomeShopEntry(
                index + 1,
                shop.DisplayName,
                shop.PathPrefix,
                shop.SeoDescription,
                shop.Theme.PrimaryColour,
                shop.Theme.AccentColour,
                previewImages.Distinct(StringComparer.Ordinal).Take(3).ToArray(),
                BuildHighlights(shop)));
        }

        Shops = entries;
    }

    private static IReadOnlyList<string> BuildHighlights(ShopDefinition shop) =>
        string.Equals(shop.Slug, "plushies", StringComparison.OrdinalIgnoreCase)
            ? ["Collectable companions", "Curious creatures", "Cosy favourites"]
            : ["Specialist finds", "Carefully curated", "Easy to explore"];

    public sealed record HomeShopEntry(
        int Order,
        string DisplayName,
        string Path,
        string Description,
        string PrimaryColour,
        string AccentColour,
        IReadOnlyList<string> PreviewImages,
        IReadOnlyList<string> Highlights);
}
