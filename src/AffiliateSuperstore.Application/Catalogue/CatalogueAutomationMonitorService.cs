using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public enum AutomationAlertSeverity
{
    Information,
    Warning,
    Critical
}

public sealed record AutomationAlert(
    AutomationAlertSeverity Severity,
    string Title,
    string Detail);

public sealed record CatalogueAutomationMonitoringReport(
    DateTimeOffset GeneratedUtc,
    int PublishedProducts,
    int FreshProducts,
    int StaleProducts,
    int NeverCheckedProducts,
    int SuspectedUnavailableProducts,
    int ConfirmedUnavailableProducts,
    int ActiveLinks,
    int StaleLinks,
    int FailedLinks,
    int FailedRunsInAlertWindow,
    int DeadLetters,
    int ExpiredLeases,
    DateTimeOffset? OldestAvailableWorkUtc,
    DateTimeOffset? LastSuccessfulRunUtc,
    DateTimeOffset? LastFailedRunUtc,
    IReadOnlyList<AutomationAlert> Alerts);

public sealed class CatalogueAutomationMonitorService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    TimeProvider timeProvider)
{
    public async Task<CatalogueAutomationMonitoringReport> GetReportAsync(
        CatalogueAutomationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var now = timeProvider.GetUtcNow();
        var staleProductBefore = now.AddHours(-options.ProductStaleAfterHours);
        var staleLinkBefore = now.AddHours(-options.LinkRefreshHours);
        var failureWindowStart = now.AddHours(-options.FailureAlertHours);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var approvedProducts = context.ShopProducts.AsNoTracking()
            .Where(item => item.Shop.IsEnabled &&
                           item.IsActive &&
                           item.ReviewStatus == ProductReviewStatus.Approved);
        var publishedProducts = approvedProducts
            .Where(item => item.Product.IsEligible &&
                           item.Product.AvailabilityState != ProductAvailabilityState.Unavailable &&
                           item.Product.AffiliateLinks.Any(link =>
                               link.ShopId == item.ShopId && link.Status == AffiliateLinkStatus.Active));

        var publishedCount = await publishedProducts.CountAsync(cancellationToken);
        var staleProducts = await publishedProducts.CountAsync(item =>
            item.Product.LastDetailRefreshedUtc == null ||
            item.Product.LastDetailRefreshedUtc < staleProductBefore,
            cancellationToken);
        var neverCheckedProducts = await publishedProducts.CountAsync(item =>
            item.Product.LastCheckedUtc == null,
            cancellationToken);
        var suspectedUnavailable = await approvedProducts.CountAsync(item =>
            item.Product.AvailabilityState == ProductAvailabilityState.SuspectedUnavailable,
            cancellationToken);
        var confirmedUnavailable = await approvedProducts.CountAsync(item =>
            item.Product.AvailabilityState == ProductAvailabilityState.Unavailable,
            cancellationToken);

        var activeLinksQuery = context.AffiliateLinks.AsNoTracking()
            .Where(link => link.Shop.IsEnabled && link.Status == AffiliateLinkStatus.Active);
        var activeLinks = await activeLinksQuery.CountAsync(cancellationToken);
        var staleLinks = await activeLinksQuery.CountAsync(link =>
            link.LastValidatedUtc == null ||
            link.LastValidatedUtc < staleLinkBefore ||
            (link.ExpiresUtc != null && link.ExpiresUtc <= now),
            cancellationToken);
        var failedLinks = await context.AffiliateLinks.AsNoTracking().CountAsync(link =>
            link.Shop.IsEnabled &&
            (link.Status == AffiliateLinkStatus.Invalid ||
             link.Status == AffiliateLinkStatus.GenerationFailed ||
             link.LastError != null),
            cancellationToken);

        var failedRuns = await context.IngestionJobs.AsNoTracking().CountAsync(job =>
            job.Status == IngestionJobStatus.Failed &&
            (job.CompletedUtc ?? job.StartedUtc ?? job.QueuedUtc) >= failureWindowStart,
            cancellationToken);
        var lastSuccessfulRun = await context.IngestionJobs.AsNoTracking()
            .Where(job => job.Status == IngestionJobStatus.Succeeded ||
                          job.Status == IngestionJobStatus.PartiallySucceeded)
            .MaxAsync(job => (DateTimeOffset?)job.CompletedUtc, cancellationToken);
        var lastFailedRun = await context.IngestionJobs.AsNoTracking()
            .Where(job => job.Status == IngestionJobStatus.Failed)
            .MaxAsync(job => (DateTimeOffset?)job.CompletedUtc, cancellationToken);

        var queueRows = await context.AutomationWorkItems.AsNoTracking()
            .Where(item => item.Status == AutomationWorkStatus.Pending ||
                           item.Status == AutomationWorkStatus.Leased ||
                           item.Status == AutomationWorkStatus.DeadLetter)
            .Select(item => new
            {
                item.Status,
                item.AvailableUtc,
                item.LeaseExpiresUtc
            })
            .ToArrayAsync(cancellationToken);
        var deadLetters = queueRows.Count(item => item.Status == AutomationWorkStatus.DeadLetter);
        var expiredLeases = queueRows.Count(item =>
            item.Status == AutomationWorkStatus.Leased && item.LeaseExpiresUtc <= now);
        var oldestAvailable = queueRows
            .Where(item => item.Status == AutomationWorkStatus.Pending && item.AvailableUtc <= now)
            .Select(item => (DateTimeOffset?)item.AvailableUtc)
            .Min();

        var alerts = BuildAlerts(
            now,
            options,
            staleProducts,
            suspectedUnavailable,
            confirmedUnavailable,
            staleLinks,
            failedLinks,
            failedRuns,
            deadLetters,
            expiredLeases,
            oldestAvailable);

        return new CatalogueAutomationMonitoringReport(
            now,
            publishedCount,
            publishedCount - staleProducts,
            staleProducts,
            neverCheckedProducts,
            suspectedUnavailable,
            confirmedUnavailable,
            activeLinks,
            staleLinks,
            failedLinks,
            failedRuns,
            deadLetters,
            expiredLeases,
            oldestAvailable,
            lastSuccessfulRun,
            lastFailedRun,
            alerts);
    }

    private static IReadOnlyList<AutomationAlert> BuildAlerts(
        DateTimeOffset now,
        CatalogueAutomationOptions options,
        int staleProducts,
        int suspectedUnavailable,
        int confirmedUnavailable,
        int staleLinks,
        int failedLinks,
        int failedRuns,
        int deadLetters,
        int expiredLeases,
        DateTimeOffset? oldestAvailable)
    {
        var alerts = new List<AutomationAlert>();
        if (deadLetters > 0)
        {
            alerts.Add(new(AutomationAlertSeverity.Critical, "Dead-letter work needs attention",
                $"{deadLetters} work item(s) exhausted all retry attempts."));
        }
        if (expiredLeases > 0)
        {
            alerts.Add(new(AutomationAlertSeverity.Critical, "Automation leases have expired",
                $"{expiredLeases} work item(s) can be recovered by the next worker cycle."));
        }
        if (oldestAvailable is not null &&
            oldestAvailable <= now.AddMinutes(-options.QueueDelayWarningMinutes))
        {
            alerts.Add(new(AutomationAlertSeverity.Critical, "Runnable work is delayed",
                $"The oldest available item has waited since {oldestAvailable.Value:O}."));
        }
        if (failedRuns > 0)
        {
            alerts.Add(new(AutomationAlertSeverity.Warning, "Recent catalogue runs failed",
                $"{failedRuns} run(s) failed in the last {options.FailureAlertHours} hours."));
        }
        if (staleProducts > 0)
        {
            alerts.Add(new(AutomationAlertSeverity.Warning, "Published product data is stale",
                $"{staleProducts} product(s) are older than {options.ProductStaleAfterHours} hours."));
        }
        if (staleLinks > 0 || failedLinks > 0)
        {
            alerts.Add(new(AutomationAlertSeverity.Warning, "Affiliate links need validation",
                $"{staleLinks} active link(s) are stale and {failedLinks} link(s) have errors."));
        }
        if (suspectedUnavailable > 0)
        {
            alerts.Add(new(AutomationAlertSeverity.Information, "Availability confirmation pending",
                $"{suspectedUnavailable} product(s) remain visible until a second direct check at least 24 hours later."));
        }
        if (confirmedUnavailable > 0)
        {
            alerts.Add(new(AutomationAlertSeverity.Information, "Unavailable products are hidden",
                $"{confirmedUnavailable} approved product(s) are excluded from public catalogue results."));
        }
        if (alerts.Count == 0)
        {
            alerts.Add(new(AutomationAlertSeverity.Information, "No operational alerts",
                "Queue, catalogue freshness, availability and affiliate-link checks are within their configured thresholds."));
        }

        return alerts;
    }
}
