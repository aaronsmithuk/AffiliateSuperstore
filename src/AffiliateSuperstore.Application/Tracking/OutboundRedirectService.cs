using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Core.Tracking;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Tracking;

public sealed record OutboundRedirectResult(string Url, string ClickId);

public sealed class OutboundRedirectService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    AffiliateTrackingService trackingService,
    TimeProvider timeProvider)
{
    private static readonly HashSet<string> AllowedPlacements =
        ["product-card", "product-page", "basket"];

    public async Task<OutboundRedirectResult?> CreateAsync(
        string shopSlug,
        string productId,
        string? placement,
        string? anonymousSessionHash = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);

        var safePlacement = AllowedPlacements.Contains(placement ?? string.Empty)
            ? placement!
            : "product-card";

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await context.ShopProducts
            .Include(record => record.Shop)
            .Include(record => record.Product)
            .ThenInclude(product => product.AffiliateLinks)
            .SingleOrDefaultAsync(record =>
                record.Shop.Slug == shopSlug &&
                record.ProductId == productId &&
                record.Shop.IsEnabled &&
                record.IsActive &&
                record.ReviewStatus == ProductReviewStatus.Approved &&
                record.Product.IsEligible,
                cancellationToken);

        if (item is null)
        {
            return null;
        }

        var link = item.Product.AffiliateLinks
            .Where(record => record.ShopId == item.ShopId && record.Status == AffiliateLinkStatus.Active)
            .OrderByDescending(record => record.GeneratedUtc)
            .FirstOrDefault();
        if (link is null)
        {
            return null;
        }

        var tracked = trackingService.Create(
            link.PromotionUrl,
            new ShopDefinition
            {
                Slug = item.Shop.Slug,
                DisplayName = item.Shop.DisplayName,
                PathPrefix = item.Shop.PathPrefix,
                TrackingId = item.Shop.TrackingId,
                SubAffiliateCode = item.Shop.SubAffiliateCode ?? string.Empty
            },
            safePlacement);

        context.OutboundClicks.Add(new OutboundClickRecord
        {
            ClickId = tracked.ClickId,
            ShopId = item.ShopId,
            ProductId = item.ProductId,
            AffiliateLinkId = link.Id,
            TrackingId = tracked.TrackingId,
            Campaign = tracked.Campaign,
            Placement = tracked.Placement,
            AnonymousSessionHash = anonymousSessionHash,
            ClickedUtc = timeProvider.GetUtcNow()
        });
        await context.SaveChangesAsync(cancellationToken);

        return new OutboundRedirectResult(tracked.Url, tracked.ClickId);
    }
}
