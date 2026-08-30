using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AffiliateSuperstore.Web.Services;

public sealed class CatalogueAutomationWorker(
    IServiceScopeFactory scopeFactory,
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    IOptions<CatalogueAutomationOptions> options,
    AffiliateSuperstoreOptions superstoreOptions,
    TimeProvider timeProvider,
    ILogger<CatalogueAutomationWorker> logger) : BackgroundService
{
    private readonly CatalogueAutomationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Catalogue automation is disabled by configuration.");
            return;
        }

        ValidateOptions();
        await RunDueJobsAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.PollEveryMinutes), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunDueJobsAsync(stoppingToken);
        }
    }

    internal async Task RunDueJobsAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var shops = await context.Shops.AsNoTracking()
            .Where(shop => shop.IsEnabled)
            .Select(shop => new { shop.Id, shop.Slug, shop.DefaultSearchQuery })
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        foreach (var shop in shops)
        {
            var configuredShop = superstoreOptions.Shops.SingleOrDefault(item =>
                string.Equals(item.Slug, shop.Slug, StringComparison.OrdinalIgnoreCase));
            if (configuredShop is null)
            {
                logger.LogWarning("Skipping scheduled catalogue ingestion for unconfigured shop {ShopSlug}.", shop.Slug);
                continue;
            }

            var latest = await context.IngestionJobs.AsNoTracking()
                .Where(job => job.ShopId == shop.Id && job.Type == IngestionJobType.CatalogueDiscovery)
                .OrderByDescending(job => job.QueuedUtc)
                .Select(job => new { job.Status, job.StartedUtc, job.CompletedUtc })
                .FirstOrDefaultAsync(cancellationToken);

            if (!CatalogueAutomationPlanner.IsDue(
                    latest?.Status,
                    latest?.StartedUtc,
                    latest?.CompletedUtc,
                    now,
                    _options))
            {
                continue;
            }

            using var scope = scopeFactory.CreateScope();
            var discovery = scope.ServiceProvider.GetRequiredService<CatalogueDiscoveryPlanService>();
            var discoveryResult = await discovery.RunAsync(shop.Slug, _options.PageSize, cancellationToken);
            logger.LogInformation(
                "Scheduled catalogue discovery plan for {ShopSlug} finished with {Status}: {Completed}/{Planned} requests, {Written} products and {Links} links.",
                shop.Slug,
                discoveryResult.Status,
                discoveryResult.RequestsCompleted,
                discoveryResult.RequestsPlanned,
                discoveryResult.ProductsWritten,
                discoveryResult.LinksCreatedOrRefreshed);

            var renewal = scope.ServiceProvider.GetRequiredService<AffiliateLinkRenewalService>();
            var renewalResult = await renewal.RunAsync(
                shop.Slug,
                TimeSpan.FromHours(_options.LinkRefreshHours),
                _options.LinkBatchSize,
                cancellationToken);
            logger.LogInformation(
                "Affiliate link renewal {JobId} for {ShopSlug} finished with {Status}: {Validated} validated, {Replaced} replaced and {Missing} missing.",
                renewalResult.JobId,
                shop.Slug,
                renewalResult.Status,
                renewalResult.Validated,
                renewalResult.Replaced,
                renewalResult.Missing);
        }
    }

    private void ValidateOptions()
    {
        if (_options.RefreshEveryHours is < 1 or > 720) throw new InvalidOperationException("CatalogueAutomation:RefreshEveryHours must be between 1 and 720.");
        if (_options.PollEveryMinutes is < 1 or > 1440) throw new InvalidOperationException("CatalogueAutomation:PollEveryMinutes must be between 1 and 1440.");
        if (_options.FailureRetryMinutes is < 1 or > 1440) throw new InvalidOperationException("CatalogueAutomation:FailureRetryMinutes must be between 1 and 1440.");
        if (_options.StaleJobHours is < 1 or > 48) throw new InvalidOperationException("CatalogueAutomation:StaleJobHours must be between 1 and 48.");
        if (_options.PageSize is < 1 or > 50) throw new InvalidOperationException("CatalogueAutomation:PageSize must be between 1 and 50.");
        if (_options.LinkRefreshHours is < 1 or > 720) throw new InvalidOperationException("CatalogueAutomation:LinkRefreshHours must be between 1 and 720.");
        if (_options.LinkBatchSize is < 1 or > 50) throw new InvalidOperationException("CatalogueAutomation:LinkBatchSize must be between 1 and 50.");
    }
}
