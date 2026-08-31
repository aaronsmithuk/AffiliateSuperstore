using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AutomationWorkQueueServiceTests
{
    [Fact]
    public async Task PlanDueAsync_IsIdempotentAndCreatesIndependentWorkTypes()
    {
        var (factory, clock, _) = await CreateDatabaseAsync();
        var service = new AutomationWorkQueueService(factory, clock);
        var options = Options();

        var first = await service.PlanDueAsync(options);
        var duplicate = await service.PlanDueAsync(options);

        Assert.Equal(3, first);
        Assert.Equal(0, duplicate);
        await using var context = factory.CreateDbContext();
        Assert.Equal(3, await context.AutomationWorkItems.CountAsync());
        Assert.Equal(3, await context.AutomationWorkItems.Select(item => item.Type).Distinct().CountAsync());
        Assert.Equal(3, await context.AutomationWorkItems.Select(item => item.IdempotencyKey).Distinct().CountAsync());
    }

    [Fact]
    public async Task ClaimNextAsync_ConcurrentClaimersLeaseOneItemOnce()
    {
        var (factory, clock, shopId) = await CreateDatabaseAsync();
        await SeedWorkAsync(factory, shopId, clock.UtcNow, maximumAttempts: 3);
        var service = new AutomationWorkQueueService(factory, clock);

        var claims = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(index => service.ClaimNextAsync($"worker-{index}", TimeSpan.FromMinutes(15))));

        var lease = Assert.Single(claims, claim => claim is not null);
        Assert.Equal(1, lease!.AttemptCount);
        await using var context = factory.CreateDbContext();
        var stored = await context.AutomationWorkItems.SingleAsync();
        Assert.Equal(AutomationWorkStatus.Leased, stored.Status);
        Assert.Equal(1, stored.AttemptCount);
    }

    [Fact]
    public async Task ExpiredLease_IsRecoveredAndFailureBackoffEndsInDeadLetter()
    {
        var (factory, clock, shopId) = await CreateDatabaseAsync();
        await SeedWorkAsync(factory, shopId, clock.UtcNow, maximumAttempts: 2);
        var service = new AutomationWorkQueueService(factory, clock);
        var options = Options();

        var first = await service.ClaimNextAsync("worker-a", TimeSpan.FromMinutes(10));
        Assert.NotNull(first);
        clock.UtcNow = clock.UtcNow.AddMinutes(11);
        var recovered = await service.ClaimNextAsync("worker-b", TimeSpan.FromMinutes(10));

        Assert.Equal(first!.Id, recovered!.Id);
        Assert.Equal(2, recovered.AttemptCount);
        var status = await service.FailAsync(recovered.Id, "worker-b", "permanent test failure", options);
        Assert.Equal(AutomationWorkStatus.DeadLetter, status);
        var health = await service.GetHealthAsync();
        Assert.Equal(1, health.DeadLetters);
        Assert.Equal(0, health.ExpiredLeases);
    }

    [Fact]
    public async Task FailAsync_UsesBoundedRetryAndRequiresLeaseOwnerToComplete()
    {
        var (factory, clock, shopId) = await CreateDatabaseAsync();
        await SeedWorkAsync(factory, shopId, clock.UtcNow, maximumAttempts: 3);
        var service = new AutomationWorkQueueService(factory, clock);
        var options = Options();
        var lease = await service.ClaimNextAsync("worker-a", TimeSpan.FromMinutes(10));

        Assert.False(await service.CompleteAsync(lease!.Id, "wrong-worker", null));
        var retryStatus = await service.FailAsync(lease.Id, "worker-a", "temporary test failure", options);

        Assert.Equal(AutomationWorkStatus.Pending, retryStatus);
        await using var context = factory.CreateDbContext();
        var stored = await context.AutomationWorkItems.SingleAsync();
        Assert.Equal(clock.UtcNow.AddMinutes(options.RetryBaseMinutes), stored.AvailableUtc);
        Assert.Null(stored.LeaseOwner);
        Assert.Null(stored.LeaseExpiresUtc);
    }

    private static CatalogueAutomationOptions Options() => new()
    {
        RefreshEveryHours = 24,
        LinkRefreshHours = 120,
        MaximumAttempts = 3,
        RetryBaseMinutes = 15,
        RetryMaximumMinutes = 60
    };

    private static async Task<(InMemoryFactory Factory, MutableTimeProvider Clock, Guid ShopId)> CreateDatabaseAsync()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 31, 8, 0, 0, TimeSpan.Zero));
        var shopId = Guid.CreateVersion7();
        await using var context = factory.CreateDbContext();
        context.Shops.Add(new ShopRecord
        {
            Id = shopId,
            Slug = "plushies",
            DisplayName = "The Plushy Shop",
            PathPrefix = "/plushies",
            TrackingId = "theplushyshop",
            DefaultSearchQuery = "plush toy",
            SeoTitle = "Plush toys",
            SeoDescription = "Curated plush toys",
            PrimaryColour = "#000000",
            AccentColour = "#ffffff",
            CreatedUtc = clock.UtcNow,
            UpdatedUtc = clock.UtcNow
        });
        await context.SaveChangesAsync();
        return (factory, clock, shopId);
    }

    private static async Task SeedWorkAsync(
        InMemoryFactory factory,
        Guid shopId,
        DateTimeOffset now,
        int maximumAttempts)
    {
        await using var context = factory.CreateDbContext();
        context.AutomationWorkItems.Add(new AutomationWorkItemRecord
        {
            Id = Guid.CreateVersion7(),
            ShopId = shopId,
            Type = AutomationWorkType.CatalogueDiscovery,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Priority = 100,
            QueuedUtc = now,
            AvailableUtc = now,
            MaximumAttempts = maximumAttempts
        });
        await context.SaveChangesAsync();
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }
}
