using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record AffiliateLinkRenewalResult(
    Guid JobId,
    IngestionJobStatus Status,
    int Candidates,
    int Validated,
    int Replaced,
    int Missing,
    string? Error);

public sealed class AffiliateLinkRenewalService(
    IAffiliateCatalogueSource source,
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    TimeProvider timeProvider)
{
    public async Task<AffiliateLinkRenewalResult> RunAsync(
        string shopSlug,
        TimeSpan maximumValidationAge,
        int batchSize = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        if (maximumValidationAge <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumValidationAge));
        if (batchSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(batchSize));

        var jobId = Guid.CreateVersion7();
        Guid shopId;
        string trackingId;
        var now = timeProvider.GetUtcNow();
        await using (var setup = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            var shop = await setup.Shops.SingleOrDefaultAsync(
                item => item.Slug == shopSlug && item.IsEnabled,
                cancellationToken) ?? throw new InvalidOperationException($"Enabled shop '{shopSlug}' was not found.");
            shopId = shop.Id;
            trackingId = shop.TrackingId;
            setup.IngestionJobs.Add(new IngestionJobRecord
            {
                Id = jobId,
                ShopId = shopId,
                Type = IngestionJobType.LinkRefresh,
                Status = IngestionJobStatus.Running,
                QueuedUtc = now,
                StartedUtc = now,
                CorrelationId = jobId.ToString("N"),
                Checkpoint = $"maximum-age-hours={maximumValidationAge.TotalHours:0.##}"
            });
            await setup.SaveChangesAsync(cancellationToken);
        }

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var staleBefore = now - maximumValidationAge;
            var candidates = await context.AffiliateLinks
                .Where(link =>
                    link.ShopId == shopId &&
                    link.ProductId != null &&
                    link.Status == AffiliateLinkStatus.Active &&
                    (link.LastValidatedUtc == null || link.LastValidatedUtc <= staleBefore))
                .OrderBy(link => link.LastValidatedUtc)
                .ToListAsync(cancellationToken);
            var validated = 0;
            var replaced = 0;
            var missing = 0;

            foreach (var batch in candidates.Chunk(batchSize))
            {
                var generated = await source.GenerateLinksAsync(
                    batch.Select(link => link.SourceUrl).Distinct(StringComparer.Ordinal).ToArray(),
                    trackingId,
                    cancellationToken);
                var bySource = generated
                    .GroupBy(link => NormaliseUrl(link.SourceUrl), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

                foreach (var current in batch)
                {
                    if (!bySource.TryGetValue(NormaliseUrl(current.SourceUrl), out var renewed))
                    {
                        current.LastError = "AliExpress did not return a renewed link for this source URL.";
                        missing++;
                        continue;
                    }

                    current.LastError = null;
                    if (string.Equals(current.PromotionUrl, renewed.PromotionUrl, StringComparison.Ordinal))
                    {
                        current.LastValidatedUtc = now;
                        validated++;
                        continue;
                    }

                    current.Status = AffiliateLinkStatus.Expired;
                    context.AffiliateLinks.Add(new AffiliateLinkRecord
                    {
                        Id = Guid.CreateVersion7(),
                        ShopId = current.ShopId,
                        ProductId = current.ProductId,
                        SourceUrl = renewed.SourceUrl,
                        PromotionUrl = renewed.PromotionUrl,
                        TrackingId = trackingId,
                        PromotionLinkType = current.PromotionLinkType,
                        Status = AffiliateLinkStatus.Active,
                        GeneratedUtc = now,
                        LastValidatedUtc = now
                    });
                    replaced++;
                }
            }

            var job = await context.IngestionJobs.SingleAsync(item => item.Id == jobId, cancellationToken);
            job.ItemsRead = candidates.Count;
            job.ItemsWritten = validated + replaced;
            job.ItemsRejected = missing;
            job.LinksCreatedOrRefreshed = validated + replaced;
            job.Status = missing == 0 ? IngestionJobStatus.Succeeded : IngestionJobStatus.PartiallySucceeded;
            job.CompletedUtc = now;
            job.Checkpoint = "complete=true";
            await context.SaveChangesAsync(cancellationToken);
            return new AffiliateLinkRenewalResult(jobId, job.Status, candidates.Count, validated, replaced, missing, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await using var failed = await contextFactory.CreateDbContextAsync(cancellationToken);
            var job = await failed.IngestionJobs.SingleAsync(item => item.Id == jobId, cancellationToken);
            job.Status = IngestionJobStatus.Failed;
            job.CompletedUtc = timeProvider.GetUtcNow();
            job.ErrorSummary = $"{exception.GetType().Name}: {exception.Message}";
            await failed.SaveChangesAsync(cancellationToken);
            return new AffiliateLinkRenewalResult(jobId, IngestionJobStatus.Failed, 0, 0, 0, 0, exception.Message);
        }
    }

    private static string NormaliseUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return value.Trim();
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }
}
