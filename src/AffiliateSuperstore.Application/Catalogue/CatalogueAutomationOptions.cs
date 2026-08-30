using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence.Entities;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed class CatalogueAutomationOptions
{
    public const string SectionName = "CatalogueAutomation";

    public bool Enabled { get; set; }
    public int RefreshEveryHours { get; set; } = 24;
    public int PollEveryMinutes { get; set; } = 15;
    public int FailureRetryMinutes { get; set; } = 60;
    public int StaleJobHours { get; set; } = 2;
    public int PageSize { get; set; } = 20;
    public int LinkRefreshHours { get; set; } = 120;
    public int LinkBatchSize { get; set; } = 50;
}

public static class CatalogueAutomationPlanner
{
    public static bool IsDue(
        IngestionJobStatus? latestStatus,
        DateTimeOffset? latestStartedUtc,
        DateTimeOffset? latestCompletedUtc,
        DateTimeOffset now,
        CatalogueAutomationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (latestStatus is null)
        {
            return true;
        }

        if (latestStatus == IngestionJobStatus.Running)
        {
            return latestStartedUtc is null || latestStartedUtc <= now.AddHours(-options.StaleJobHours);
        }

        var latestActivity = latestCompletedUtc ?? latestStartedUtc;
        if (latestActivity is null)
        {
            return true;
        }

        var delay = latestStatus == IngestionJobStatus.Failed
            ? TimeSpan.FromMinutes(options.FailureRetryMinutes)
            : TimeSpan.FromHours(options.RefreshEveryHours);
        return latestActivity <= now - delay;
    }
}

public static class CatalogueDiscoveryPlanner
{
    public static IReadOnlyList<CatalogueIngestionRequest> Build(
        ShopDefinition shop,
        int pageSize)
    {
        ArgumentNullException.ThrowIfNull(shop);
        if (pageSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(pageSize));

        var queries = shop.DiscoveryQueries
            .Select(query => query.Trim())
            .Where(query => query.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (queries.Length == 0) queries = [shop.DefaultSearchQuery.Trim()];

        return queries
            .SelectMany(query => Enumerable.Range(1, shop.DiscoveryPagesPerQuery)
                .Select(page => new CatalogueIngestionRequest(shop.Slug, query, page, pageSize)))
            .ToArray();
    }
}
