using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record AutomationWorkLease(
    Guid Id,
    AutomationWorkType Type,
    Guid? ShopId,
    string? ShopSlug,
    string IdempotencyKey,
    int AttemptCount,
    int MaximumAttempts,
    DateTimeOffset LeaseExpiresUtc);

public sealed record AutomationQueueHealth(
    int Pending,
    int Leased,
    int RetryWaiting,
    int ExpiredLeases,
    int DeadLetters,
    DateTimeOffset? OldestAvailableUtc);

public sealed class AutomationWorkQueueService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    TimeProvider timeProvider,
    AutonomousCatalogueOptions autonomousOptions)
{
    private static readonly SemaphoreSlim MutationGate = new(1, 1);

    public async Task<int> PlanDueAsync(
        CatalogueAutomationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var now = timeProvider.GetUtcNow();
        var planned = 0;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var shops = await context.Shops.AsNoTracking()
            .Where(shop => shop.IsEnabled)
            .Select(shop => new { shop.Id, shop.Slug })
            .ToArrayAsync(cancellationToken);
        var autonomousPolicies = autonomousOptions.Enabled
            ? await context.AutonomousCataloguePolicies.AsNoTracking()
                .Where(policy => policy.Mode != AutonomousCatalogueMode.Off)
                .Select(policy => new { policy.ShopId, policy.Mode, policy.ReviewEveryHours })
                .ToDictionaryAsync(policy => policy.ShopId, cancellationToken)
            : [];

        foreach (var shop in shops)
        {
            planned += await EnqueueIfDueAsync(
                shop.Id, shop.Slug, AutomationWorkType.CatalogueDiscovery,
                TimeSpan.FromHours(options.RefreshEveryHours), 100, options.MaximumAttempts, now, cancellationToken);
            planned += await EnqueueIfDueAsync(
                shop.Id, shop.Slug, AutomationWorkType.ProductRefresh,
                TimeSpan.FromHours(options.RefreshEveryHours), 80, options.MaximumAttempts, now, cancellationToken);
            planned += await EnqueueIfDueAsync(
                shop.Id, shop.Slug, AutomationWorkType.IdentityRefresh,
                TimeSpan.FromHours(options.RefreshEveryHours), 70, options.MaximumAttempts, now, cancellationToken);
            planned += await EnqueueIfDueAsync(
                shop.Id, shop.Slug, AutomationWorkType.LinkRefresh,
                TimeSpan.FromHours(options.LinkRefreshHours), 60, options.MaximumAttempts, now, cancellationToken);
            if (autonomousPolicies.TryGetValue(shop.Id, out var policy))
            {
                if (policy.Mode == AutonomousCatalogueMode.Automatic &&
                    autonomousOptions.AutomaticCollectionGrowthEnabled)
                {
                    planned += await EnqueueIfDueAsync(
                        shop.Id, shop.Slug, AutomationWorkType.CollectionGrowth,
                        TimeSpan.FromHours(Math.Clamp(autonomousOptions.CollectionGrowthEveryHours, 1, 168)), 55,
                        options.MaximumAttempts, now, cancellationToken);
                }
                planned += await EnqueueIfDueAsync(
                    shop.Id, shop.Slug, AutomationWorkType.AutonomousReview,
                    TimeSpan.FromHours(Math.Clamp(policy.ReviewEveryHours, 1, 720)), 50,
                    options.MaximumAttempts, now, cancellationToken);
            }
            if (autonomousOptions.CollectionSuggestionsEnabled)
            {
                planned += await EnqueueIfDueAsync(
                    shop.Id, shop.Slug, AutomationWorkType.CollectionSuggestions,
                    TimeSpan.FromDays(Math.Clamp(autonomousOptions.CollectionSuggestionEveryDays, 1, 31)), 40,
                    options.MaximumAttempts, now, cancellationToken);
            }
        }

        return planned;
    }

    public async Task<AutomationWorkLease?> ClaimNextAsync(
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        await MutationGate.WaitAsync(cancellationToken);
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
                var now = timeProvider.GetUtcNow();
                var item = await context.AutomationWorkItems
                    .Include(work => work.Shop)
                    .Where(work =>
                        (work.Status == AutomationWorkStatus.Pending && work.AvailableUtc <= now) ||
                        (work.Status == AutomationWorkStatus.Leased && work.LeaseExpiresUtc <= now))
                    .OrderByDescending(work => work.Priority)
                    .ThenBy(work => work.AvailableUtc)
                    .ThenBy(work => work.QueuedUtc)
                    .FirstOrDefaultAsync(cancellationToken);
                if (item is null) return null;

                item.Status = AutomationWorkStatus.Leased;
                item.LeaseOwner = leaseOwner.Trim();
                item.LeaseExpiresUtc = now + leaseDuration;
                item.AttemptCount++;
                item.StartedUtc ??= now;
                item.CorrelationId ??= item.Id.ToString("N");
                item.Checkpoint = $"leased:attempt={item.AttemptCount}";
                try
                {
                    await context.SaveChangesAsync(cancellationToken);
                    return new AutomationWorkLease(
                        item.Id,
                        item.Type,
                        item.ShopId,
                        item.Shop?.Slug,
                        item.IdempotencyKey,
                        item.AttemptCount,
                        item.MaximumAttempts,
                        item.LeaseExpiresUtc.Value);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Another application instance won the row-version race; try the next item.
                }
            }

            return null;
        }
        finally
        {
            MutationGate.Release();
        }
    }

    public async Task<bool> CompleteAsync(
        Guid workItemId,
        string leaseOwner,
        Guid? resultJobId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await context.AutomationWorkItems.SingleOrDefaultAsync(
            work => work.Id == workItemId &&
                    work.Status == AutomationWorkStatus.Leased &&
                    work.LeaseOwner == leaseOwner,
            cancellationToken);
        if (item is null) return false;

        item.Status = AutomationWorkStatus.Succeeded;
        item.CompletedUtc = timeProvider.GetUtcNow();
        item.ResultJobId = resultJobId;
        item.LeaseOwner = null;
        item.LeaseExpiresUtc = null;
        item.LastError = null;
        item.Checkpoint = "completed";
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RenewLeaseAsync(
        Guid workItemId,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await context.AutomationWorkItems.SingleOrDefaultAsync(
            work => work.Id == workItemId &&
                    work.Status == AutomationWorkStatus.Leased &&
                    work.LeaseOwner == leaseOwner,
            cancellationToken);
        if (item is null) return false;

        var now = timeProvider.GetUtcNow();
        item.LeaseExpiresUtc = now + leaseDuration;
        item.Checkpoint = $"lease-renewed:attempt={item.AttemptCount};expires={item.LeaseExpiresUtc:O}";
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<bool> RequeueDeadLetterAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await context.AutomationWorkItems.SingleOrDefaultAsync(
            work => work.Id == workItemId && work.Status == AutomationWorkStatus.DeadLetter,
            cancellationToken);
        if (item is null) return false;

        var now = timeProvider.GetUtcNow();
        item.Status = AutomationWorkStatus.Pending;
        item.AvailableUtc = now;
        item.AttemptCount = 0;
        item.StartedUtc = null;
        item.CompletedUtc = null;
        item.LeaseOwner = null;
        item.LeaseExpiresUtc = null;
        item.LastError = null;
        item.ResultJobId = null;
        item.Checkpoint = $"operator-requeued:{now:O}";
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AutomationWorkStatus?> FailAsync(
        Guid workItemId,
        string leaseOwner,
        string error,
        CatalogueAutomationOptions options,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await context.AutomationWorkItems.SingleOrDefaultAsync(
            work => work.Id == workItemId &&
                    work.Status == AutomationWorkStatus.Leased &&
                    work.LeaseOwner == leaseOwner,
            cancellationToken);
        if (item is null) return null;

        var now = timeProvider.GetUtcNow();
        item.LastError = error.Length <= 2000 ? error : error[..2000];
        item.LeaseOwner = null;
        item.LeaseExpiresUtc = null;
        if (item.AttemptCount >= item.MaximumAttempts)
        {
            item.Status = AutomationWorkStatus.DeadLetter;
            item.CompletedUtc = now;
            item.Checkpoint = $"dead-letter:attempt={item.AttemptCount}";
        }
        else
        {
            item.Status = AutomationWorkStatus.Pending;
            item.AvailableUtc = now + RetryDelay(item.AttemptCount, options);
            item.Checkpoint = $"retry:attempt={item.AttemptCount};available={item.AvailableUtc:O}";
        }

        await context.SaveChangesAsync(cancellationToken);
        return item.Status;
    }

    public async Task<AutomationQueueHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var rows = await context.AutomationWorkItems.AsNoTracking()
            .Where(item => item.Status == AutomationWorkStatus.Pending ||
                           item.Status == AutomationWorkStatus.Leased ||
                           item.Status == AutomationWorkStatus.DeadLetter)
            .Select(item => new { item.Status, item.AvailableUtc, item.LeaseExpiresUtc, item.AttemptCount })
            .ToArrayAsync(cancellationToken);
        var available = rows
            .Where(item => item.Status == AutomationWorkStatus.Pending && item.AvailableUtc <= now)
            .Select(item => (DateTimeOffset?)item.AvailableUtc)
            .Min();
        return new AutomationQueueHealth(
            rows.Count(item => item.Status == AutomationWorkStatus.Pending),
            rows.Count(item => item.Status == AutomationWorkStatus.Leased),
            rows.Count(item => item.Status == AutomationWorkStatus.Pending && item.AvailableUtc > now && item.AttemptCount > 0),
            rows.Count(item => item.Status == AutomationWorkStatus.Leased && item.LeaseExpiresUtc <= now),
            rows.Count(item => item.Status == AutomationWorkStatus.DeadLetter),
            available);
    }

    private async Task<int> EnqueueIfDueAsync(
        Guid shopId,
        string shopSlug,
        AutomationWorkType type,
        TimeSpan cadence,
        int priority,
        int maximumAttempts,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await MutationGate.WaitAsync(cancellationToken);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var latest = await context.AutomationWorkItems.AsNoTracking()
                .Where(item => item.ShopId == shopId && item.Type == type)
                .OrderByDescending(item => item.QueuedUtc)
                .Select(item => new { item.Status, item.QueuedUtc, item.CompletedUtc })
                .FirstOrDefaultAsync(cancellationToken);
            if (latest is not null)
            {
                if (latest.Status is AutomationWorkStatus.Pending or AutomationWorkStatus.Leased) return 0;
                var activity = latest.CompletedUtc ?? latest.QueuedUtc;
                if (activity + cadence > now) return 0;
            }

            var bucket = now.UtcTicks / cadence.Ticks;
            var idempotencyKey = $"{shopSlug.Trim().ToLowerInvariant()}:{type.ToString().ToLowerInvariant()}:{bucket}";
            if (await context.AutomationWorkItems.AnyAsync(
                    item => item.IdempotencyKey == idempotencyKey,
                    cancellationToken)) return 0;

            context.AutomationWorkItems.Add(new AutomationWorkItemRecord
            {
                Id = Guid.CreateVersion7(),
                ShopId = shopId,
                Type = type,
                IdempotencyKey = idempotencyKey,
                Priority = priority,
                QueuedUtc = now,
                AvailableUtc = now,
                MaximumAttempts = maximumAttempts,
                CorrelationId = Guid.CreateVersion7().ToString("N")
            });
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return 1;
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // The unique idempotency key is the final cross-process guard.
                return 0;
            }
        }
        finally
        {
            MutationGate.Release();
        }
    }

    private static TimeSpan RetryDelay(int attemptCount, CatalogueAutomationOptions options)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 20);
        var minutes = options.RetryBaseMinutes * Math.Pow(2, exponent);
        return TimeSpan.FromMinutes(Math.Min(minutes, options.RetryMaximumMinutes));
    }
}
