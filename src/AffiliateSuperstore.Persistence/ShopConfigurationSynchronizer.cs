using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Persistence;

public sealed class ShopConfigurationSynchronizer(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    AffiliateSuperstoreOptions options,
    TimeProvider timeProvider)
{
    public async Task<int> SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var configuredSlugs = options.Shops
            .Select(shop => shop.Slug)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var records = await context.Shops.ToListAsync(cancellationToken);
        var changed = 0;

        foreach (var configured in options.Shops)
        {
            var record = records.FirstOrDefault(item =>
                string.Equals(item.Slug, configured.Slug, StringComparison.OrdinalIgnoreCase));
            var now = timeProvider.GetUtcNow();
            var isNew = record is null;

            if (record is null)
            {
                record = new ShopRecord
                {
                    Id = Guid.CreateVersion7(),
                    Slug = configured.Slug.Trim().ToLowerInvariant(),
                    CreatedUtc = now
                };
                context.Shops.Add(record);
                records.Add(record);
            }

            var trackingId = string.IsNullOrWhiteSpace(configured.TrackingId)
                ? options.FallbackTrackingId
                : configured.TrackingId;
            if (string.IsNullOrWhiteSpace(trackingId))
            {
                throw new InvalidOperationException(
                    $"Shop '{configured.Slug}' needs a dedicated Tracking ID or Superstore:FallbackTrackingId.");
            }

            var pathPrefix = NormalisePath(configured.PathPrefix);
            var canonicalHostname = configured.Hostnames.FirstOrDefault()?.Trim().ToLowerInvariant();
            var subAffiliateCode = NullIfWhiteSpace(configured.SubAffiliateCode);
            if (!isNew &&
                record.DisplayName == configured.DisplayName.Trim() &&
                record.PathPrefix == pathPrefix &&
                record.CanonicalHostname == canonicalHostname &&
                record.TrackingId == trackingId.Trim() &&
                record.SubAffiliateCode == subAffiliateCode &&
                record.DefaultSearchQuery == configured.DefaultSearchQuery.Trim() &&
                record.SeoTitle == configured.SeoTitle.Trim() &&
                record.SeoDescription == configured.SeoDescription.Trim() &&
                record.PrimaryColour == configured.Theme.PrimaryColour.Trim() &&
                record.AccentColour == configured.Theme.AccentColour.Trim() &&
                record.IsEnabled == configured.IsEnabled)
            {
                continue;
            }

            record.DisplayName = configured.DisplayName.Trim();
            record.PathPrefix = pathPrefix;
            record.CanonicalHostname = canonicalHostname;
            record.TrackingId = trackingId.Trim();
            record.SubAffiliateCode = subAffiliateCode;
            record.DefaultSearchQuery = configured.DefaultSearchQuery.Trim();
            record.SeoTitle = configured.SeoTitle.Trim();
            record.SeoDescription = configured.SeoDescription.Trim();
            record.PrimaryColour = configured.Theme.PrimaryColour.Trim();
            record.AccentColour = configured.Theme.AccentColour.Trim();
            record.IsEnabled = configured.IsEnabled;
            record.UpdatedUtc = now;
            changed++;
        }

        foreach (var stale in records.Where(record => !configuredSlugs.Contains(record.Slug) && record.IsEnabled))
        {
            stale.IsEnabled = false;
            stale.UpdatedUtc = timeProvider.GetUtcNow();
            changed++;
        }

        await context.SaveChangesAsync(cancellationToken);
        return changed;
    }

    private static string NormalisePath(string path)
    {
        var value = path.Trim();
        if (!value.StartsWith('/')) value = "/" + value;
        return value.Length == 1 ? value : value.TrimEnd('/');
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
