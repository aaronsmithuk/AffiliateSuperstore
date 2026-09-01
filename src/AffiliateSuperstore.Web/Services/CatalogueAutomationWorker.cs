using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.Extensions.Options;

namespace AffiliateSuperstore.Web.Services;

public sealed class CatalogueAutomationWorker(
    IServiceScopeFactory scopeFactory,
    AutomationWorkQueueService workQueue,
    CatalogueAutomationWakeSignal wakeSignal,
    IOptions<CatalogueAutomationOptions> options,
    TimeProvider timeProvider,
    ILogger<CatalogueAutomationWorker> logger) : BackgroundService
{
    private readonly CatalogueAutomationOptions _options = options.Value;
    private readonly string _leaseOwner = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Catalogue automation is disabled by configuration.");
            return;
        }

        ValidateOptions();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDueJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Catalogue automation cycle failed; the worker will retry after the normal poll interval.");
            }
            await WaitForPollOrWakeAsync(stoppingToken);
        }
    }

    internal async Task RunDueJobsAsync(CancellationToken cancellationToken)
    {
        var planned = await workQueue.PlanDueAsync(_options, cancellationToken);
        if (planned > 0)
        {
            logger.LogInformation("Planned {WorkItemCount} durable catalogue work items.", planned);
        }

        for (var index = 0; index < _options.MaximumWorkItemsPerTick; index++)
        {
            var lease = await workQueue.ClaimNextAsync(
                _leaseOwner,
                TimeSpan.FromMinutes(_options.LeaseMinutes),
                cancellationToken);
            if (lease is null) break;

            try
            {
                using var leaseCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var renewal = RenewLeaseUntilCancelledAsync(lease, leaseCancellation.Token);
                Guid? resultJobId;
                try
                {
                    resultJobId = await ExecuteLeaseAsync(lease, cancellationToken);
                }
                finally
                {
                    await leaseCancellation.CancelAsync();
                    try
                    {
                        await renewal;
                    }
                    catch (OperationCanceledException) when (leaseCancellation.IsCancellationRequested)
                    {
                        // Expected when the work finishes before the next renewal interval.
                    }
                }

                if (!await workQueue.CompleteAsync(lease.Id, _leaseOwner, resultJobId, cancellationToken))
                {
                    logger.LogWarning("Work item {WorkItemId} completed after its lease was lost.", lease.Id);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var status = await workQueue.FailAsync(
                    lease.Id,
                    _leaseOwner,
                    $"{exception.GetType().Name}: {exception.Message}",
                    _options,
                    cancellationToken);
                logger.LogError(
                    exception,
                    "Automation work item {WorkItemId} ({WorkType}) failed on attempt {Attempt}/{MaximumAttempts}; queue status is {QueueStatus}.",
                    lease.Id,
                    lease.Type,
                    lease.AttemptCount,
                    lease.MaximumAttempts,
                    status);
            }
        }
    }

    private async Task RenewLeaseUntilCancelledAsync(
        AutomationWorkLease lease,
        CancellationToken cancellationToken)
    {
        var leaseDuration = TimeSpan.FromMinutes(_options.LeaseMinutes);
        var renewalInterval = TimeSpan.FromTicks(Math.Max(
            TimeSpan.FromMinutes(1).Ticks,
            leaseDuration.Ticks / 3));

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(renewalInterval, timeProvider, cancellationToken);
            if (!await workQueue.RenewLeaseAsync(
                    lease.Id,
                    _leaseOwner,
                    leaseDuration,
                    cancellationToken))
            {
                logger.LogWarning("Automation work item {WorkItemId} lost its lease during execution.", lease.Id);
                return;
            }
        }
    }

    private async Task<Guid?> ExecuteLeaseAsync(AutomationWorkLease lease, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(lease.ShopSlug))
        {
            throw new InvalidOperationException($"Work item {lease.Id} has no enabled shop.");
        }

        using var scope = scopeFactory.CreateScope();
        switch (lease.Type)
        {
            case AutomationWorkType.CatalogueDiscovery:
            {
                var service = scope.ServiceProvider.GetRequiredService<CatalogueDiscoveryPlanService>();
                var result = await service.RunAsync(lease.ShopSlug, _options.PageSize, cancellationToken);
                if (result.Status is IngestionJobStatus.Failed or IngestionJobStatus.Running)
                {
                    throw new InvalidOperationException(result.Error ?? $"Discovery finished with {result.Status}.");
                }

                logger.LogInformation(
                    "Durable discovery for {ShopSlug} completed {Completed}/{Planned} requests and wrote {Written} products.",
                    lease.ShopSlug,
                    result.RequestsCompleted,
                    result.RequestsPlanned,
                    result.ProductsWritten);
                return result.Runs.LastOrDefault()?.JobId;
            }
            case AutomationWorkType.ProductRefresh:
            {
                var service = scope.ServiceProvider.GetRequiredService<CatalogueProductEnrichmentService>();
                var result = await service.RunAsync(lease.ShopSlug, cancellationToken: cancellationToken);
                if (result.Status == IngestionJobStatus.Failed)
                {
                    throw new InvalidOperationException(result.Error ?? "Product refresh failed.");
                }

                logger.LogInformation(
                    "Durable product refresh {JobId} for {ShopSlug} checked {Enriched}/{Selected} products.",
                    result.JobId,
                    lease.ShopSlug,
                    result.ProductsEnriched,
                    result.ProductsSelected);
                return result.JobId;
            }
            case AutomationWorkType.IdentityRefresh:
            {
                var service = scope.ServiceProvider.GetRequiredService<ProductIdentityService>();
                var result = await service.RebuildAsync(lease.ShopSlug, cancellationToken: cancellationToken);
                logger.LogInformation(
                    "Durable identity refresh for {ShopSlug} read {ProductsRead} products, fingerprinted {ImagesFingerprinted} images, updated {ProfilesUpdated} profiles and created {CandidatesCreated} candidates.",
                    lease.ShopSlug,
                    result.ProductsRead,
                    result.ImageFingerprintsCreated,
                    result.ProfilesUpdated,
                    result.CandidatesCreated);
                return null;
            }
            case AutomationWorkType.LinkRefresh:
            {
                var service = scope.ServiceProvider.GetRequiredService<AffiliateLinkRenewalService>();
                var result = await service.RunAsync(
                    lease.ShopSlug,
                    TimeSpan.FromHours(_options.LinkRefreshHours),
                    _options.LinkBatchSize,
                    cancellationToken);
                if (result.Status == IngestionJobStatus.Failed)
                {
                    throw new InvalidOperationException(result.Error ?? "Link refresh failed.");
                }

                logger.LogInformation(
                    "Durable link refresh {JobId} for {ShopSlug} validated {Validated} and replaced {Replaced} links.",
                    result.JobId,
                    lease.ShopSlug,
                    result.Validated,
                    result.Replaced);
                return result.JobId;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(lease), lease.Type, "Unsupported automation work type.");
        }
    }

    private async Task WaitForPollOrWakeAsync(CancellationToken cancellationToken)
    {
        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delay = Task.Delay(TimeSpan.FromMinutes(_options.PollEveryMinutes), timeProvider, waitCancellation.Token);
        var wake = wakeSignal.WaitAsync(waitCancellation.Token);
        await Task.WhenAny(delay, wake);
        await waitCancellation.CancelAsync();
    }

    private void ValidateOptions()
    {
        if (_options.RefreshEveryHours is < 1 or > 720) throw new InvalidOperationException("CatalogueAutomation:RefreshEveryHours must be between 1 and 720.");
        if (_options.PollEveryMinutes is < 1 or > 1440) throw new InvalidOperationException("CatalogueAutomation:PollEveryMinutes must be between 1 and 1440.");
        if (_options.PageSize is < 1 or > 50) throw new InvalidOperationException("CatalogueAutomation:PageSize must be between 1 and 50.");
        if (_options.LinkRefreshHours is < 1 or > 720) throw new InvalidOperationException("CatalogueAutomation:LinkRefreshHours must be between 1 and 720.");
        if (_options.LinkBatchSize is < 1 or > 50) throw new InvalidOperationException("CatalogueAutomation:LinkBatchSize must be between 1 and 50.");
        if (_options.MaximumWorkItemsPerTick is < 1 or > 20) throw new InvalidOperationException("CatalogueAutomation:MaximumWorkItemsPerTick must be between 1 and 20.");
        if (_options.LeaseMinutes is < 1 or > 120) throw new InvalidOperationException("CatalogueAutomation:LeaseMinutes must be between 1 and 120.");
        if (_options.MaximumAttempts is < 1 or > 20) throw new InvalidOperationException("CatalogueAutomation:MaximumAttempts must be between 1 and 20.");
        if (_options.RetryBaseMinutes is < 1 or > 1440) throw new InvalidOperationException("CatalogueAutomation:RetryBaseMinutes must be between 1 and 1440.");
        if (_options.RetryMaximumMinutes < _options.RetryBaseMinutes || _options.RetryMaximumMinutes > 10080) throw new InvalidOperationException("CatalogueAutomation:RetryMaximumMinutes must be between RetryBaseMinutes and 10080.");
        if (_options.ProductStaleAfterHours < _options.RefreshEveryHours || _options.ProductStaleAfterHours > 1440) throw new InvalidOperationException("CatalogueAutomation:ProductStaleAfterHours must be between RefreshEveryHours and 1440.");
        if (_options.QueueDelayWarningMinutes is < 1 or > 10080) throw new InvalidOperationException("CatalogueAutomation:QueueDelayWarningMinutes must be between 1 and 10080.");
        if (_options.FailureAlertHours is < 1 or > 720) throw new InvalidOperationException("CatalogueAutomation:FailureAlertHours must be between 1 and 720.");
    }
}
