using AffiliateSuperstore.Application.Orders;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Web.Services;

public sealed class OrderReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    OrderReconciliationOptions options,
    TimeProvider timeProvider,
    ILogger<OrderReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Order reconciliation is disabled by configuration.");
            return;
        }

        OrderReconciliationPlanner.Validate(options);
        await RunIfDueAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunIfDueAsync(stoppingToken);
        }
    }

    internal async Task RunIfDueAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var latest = await context.IngestionJobs.AsNoTracking()
            .Where(job => job.Type == IngestionJobType.OrderReconciliation)
            .OrderByDescending(job => job.QueuedUtc)
            .Select(job => new { job.Status, job.StartedUtc, job.CompletedUtc })
            .FirstOrDefaultAsync(cancellationToken);
        var activity = latest?.CompletedUtc ?? latest?.StartedUtc;
        if (!OrderReconciliationPlanner.IsDue(latest?.Status, activity, timeProvider.GetUtcNow(), options)) return;

        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AffiliateOrderReconciliationService>();
        var result = await service.RunAsync(cancellationToken);
        if (result.Status == IngestionJobStatus.Failed)
        {
            logger.LogWarning(
                "Order reconciliation {JobId} failed after reading {Read} orders: {Error}",
                result.JobId,
                result.OrdersRead,
                result.Error);
            return;
        }

        logger.LogInformation(
            "Order reconciliation {JobId} finished with {Status}: {Read} read, {Written} written, {Attributed} attributed.",
            result.JobId,
            result.Status,
            result.OrdersRead,
            result.OrdersWritten,
            result.AttributedOrders);
    }
}
