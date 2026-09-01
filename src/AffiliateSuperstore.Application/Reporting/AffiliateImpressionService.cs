using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Reporting;

public static class AffiliateImpressionPlacements
{
    public const string ProductCard = "product-card";
    public const string ProductPage = "product-page";
    public const string Basket = "basket";

    public static bool IsAllowed(string value) => value is ProductCard or ProductPage or Basket;
}

public sealed record AffiliateImpressionInput(string Shop, string ProductId, string Placement);

public sealed record AffiliateImpressionResult(int Submitted, int Accepted, int Rejected);

public sealed class AffiliateImpressionService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    TimeProvider timeProvider)
{
    public const int MaximumBatchSize = 64;

    public async Task<AffiliateImpressionResult> RecordAsync(
        IReadOnlyCollection<AffiliateImpressionInput> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Count == 0) return new AffiliateImpressionResult(0, 0, 0);
        if (source.Count > MaximumBatchSize)
        {
            throw new ArgumentOutOfRangeException(nameof(source), $"A batch cannot exceed {MaximumBatchSize} impressions.");
        }

        var submitted = source.Count;
        var candidates = source
            .Where(IsStructurallyValid)
            .Select(item => new AffiliateImpressionInput(item.Shop.Trim(), item.ProductId.Trim(), item.Placement.Trim()))
            .DistinctBy(item => (item.Shop.ToUpperInvariant(), item.ProductId, item.Placement))
            .ToArray();

        if (candidates.Length == 0) return new AffiliateImpressionResult(submitted, 0, submitted);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var shops = candidates.Select(item => item.Shop).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var productIds = candidates.Select(item => item.ProductId).Distinct(StringComparer.Ordinal).ToArray();
        var eligible = await context.ShopProducts.AsNoTracking()
            .Where(item =>
                shops.Contains(item.Shop.Slug) &&
                productIds.Contains(item.ProductId) &&
                item.Shop.IsEnabled &&
                item.IsActive &&
                item.ReviewStatus == ProductReviewStatus.Approved &&
                item.Product.IsEligible &&
                item.Product.AffiliateLinks.Any(link =>
                    link.ShopId == item.ShopId && link.Status == AffiliateLinkStatus.Active))
            .Select(item => new { item.ShopId, item.Shop.Slug, item.ProductId })
            .ToListAsync(cancellationToken);
        var eligibleLookup = eligible.ToDictionary(
            item => (item.Slug.ToUpperInvariant(), item.ProductId),
            item => item.ShopId);
        var accepted = candidates
            .Where(item => eligibleLookup.ContainsKey((item.Shop.ToUpperInvariant(), item.ProductId)))
            .ToArray();

        if (accepted.Length == 0) return new AffiliateImpressionResult(submitted, 0, submitted);

        var now = timeProvider.GetUtcNow();
        var dateUtc = DateOnly.FromDateTime(now.UtcDateTime);
        if (context.Database.IsRelational())
        {
            foreach (var item in accepted)
            {
                var shopId = eligibleLookup[(item.Shop.ToUpperInvariant(), item.ProductId)];
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE [ProductImpressions] WITH (UPDLOCK, SERIALIZABLE)
                    SET [Count] = [Count] + 1, [LastSeenUtc] = {now}
                    WHERE [ShopId] = {shopId}
                      AND [ProductId] = {item.ProductId}
                      AND [DateUtc] = {dateUtc}
                      AND [Placement] = {item.Placement};
                    IF @@ROWCOUNT = 0
                    BEGIN
                        INSERT INTO [ProductImpressions]
                            ([ShopId], [ProductId], [DateUtc], [Placement], [Count], [FirstSeenUtc], [LastSeenUtc])
                        VALUES
                            ({shopId}, {item.ProductId}, {dateUtc}, {item.Placement}, 1, {now}, {now});
                    END
                    """, cancellationToken);
            }

            return new AffiliateImpressionResult(submitted, accepted.Length, submitted - accepted.Length);
        }

        var acceptedProductIds = accepted.Select(item => item.ProductId).Distinct(StringComparer.Ordinal).ToArray();
        var placements = accepted.Select(item => item.Placement).Distinct(StringComparer.Ordinal).ToArray();
        var existing = await context.ProductImpressions
            .Where(item =>
                item.DateUtc == dateUtc &&
                acceptedProductIds.Contains(item.ProductId) &&
                placements.Contains(item.Placement))
            .ToListAsync(cancellationToken);
        var existingLookup = existing.ToDictionary(
            item => (item.ShopId, item.ProductId, item.Placement));

        foreach (var item in accepted)
        {
            var shopId = eligibleLookup[(item.Shop.ToUpperInvariant(), item.ProductId)];
            var key = (shopId, item.ProductId, item.Placement);
            if (existingLookup.TryGetValue(key, out var aggregate))
            {
                aggregate.Count++;
                aggregate.LastSeenUtc = now;
                continue;
            }

            aggregate = new ProductImpressionDailyRecord
            {
                ShopId = shopId,
                ProductId = item.ProductId,
                DateUtc = dateUtc,
                Placement = item.Placement,
                Count = 1,
                FirstSeenUtc = now,
                LastSeenUtc = now
            };
            context.ProductImpressions.Add(aggregate);
            existingLookup.Add(key, aggregate);
        }

        await context.SaveChangesAsync(cancellationToken);
        return new AffiliateImpressionResult(submitted, accepted.Length, submitted - accepted.Length);
    }

    private static bool IsStructurallyValid(AffiliateImpressionInput item) =>
        !string.IsNullOrWhiteSpace(item.Shop) && item.Shop.Trim().Length <= 80 &&
        !string.IsNullOrWhiteSpace(item.ProductId) && item.ProductId.Trim().Length <= 64 &&
        !string.IsNullOrWhiteSpace(item.Placement) && item.Placement.Trim().Length <= 100 &&
        AffiliateImpressionPlacements.IsAllowed(item.Placement.Trim());
}
