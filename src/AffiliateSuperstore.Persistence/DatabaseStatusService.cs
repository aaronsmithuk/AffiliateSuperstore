using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Persistence;

public sealed record DatabaseStatusReport(
    bool CanConnect,
    string Provider,
    string DatabaseName,
    IReadOnlyList<string> AppliedMigrations,
    IReadOnlyList<string> PendingMigrations,
    int ShopCount,
    int ProductCount,
    int SnapshotCount,
    int LinkCount,
    int ClickCount,
    int OrderCount,
    int QueuedOrRunningJobCount,
    DateTimeOffset CheckedUtc,
    string? Error);

public sealed class DatabaseStatusService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    TimeProvider timeProvider)
{
    public async Task<DatabaseStatusReport> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var provider = context.Database.ProviderName ?? "Unknown";
        var databaseName = context.Database.GetDbConnection().Database;

        try
        {
            var canConnect = await context.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return Empty(provider, databaseName, "The configured database did not accept a connection.");
            }

            var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

            return new DatabaseStatusReport(
                true,
                provider,
                databaseName,
                applied,
                pending,
                await context.Shops.CountAsync(cancellationToken),
                await context.Products.CountAsync(cancellationToken),
                await context.ProductSnapshots.CountAsync(cancellationToken),
                await context.AffiliateLinks.CountAsync(cancellationToken),
                await context.OutboundClicks.CountAsync(cancellationToken),
                await context.AffiliateOrders.CountAsync(cancellationToken),
                await context.IngestionJobs.CountAsync(
                    job => job.Status == Entities.IngestionJobStatus.Queued ||
                           job.Status == Entities.IngestionJobStatus.Running,
                    cancellationToken),
                timeProvider.GetUtcNow(),
                null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Empty(provider, databaseName, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private DatabaseStatusReport Empty(string provider, string databaseName, string error) =>
        new(false, provider, databaseName, [], [], 0, 0, 0, 0, 0, 0, 0, timeProvider.GetUtcNow(), error);
}
