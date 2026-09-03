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
        var service = Service(factory, clock);
        var options = Options();

        var first = await service.PlanDueAsync(options);
        var duplicate = await service.PlanDueAsync(options);

        Assert.Equal(4, first);
        Assert.Equal(0, duplicate);
        await using var context = factory.CreateDbContext();
        Assert.Equal(4, await context.AutomationWorkItems.CountAsync());
        Assert.Equal(4, await context.AutomationWorkItems.Select(item => item.Type).Distinct().CountAsync());
        Assert.Equal(4, await context.AutomationWorkItems.Select(item => item.IdempotencyKey).Distinct().CountAsync());
    }

    [Fact]
    public async Task PlanDueAsync_AddsAutonomousReviewOnlyForActivePolicy()
    {
        var (factory, clock, shopId) = await CreateDatabaseAsync();
        await using (var context = factory.CreateDbContext())
        {
            context.AutonomousCataloguePolicies.Add(new AutonomousCataloguePolicyRecord
            {
                ShopId = shopId,
                Mode = AutonomousCatalogueMode.Shadow,
                ReviewEveryHours = 12,
                CreatedUtc = clock.UtcNow,
                UpdatedUtc = clock.UtcNow,
                UpdatedBy = "test"
            });
            await context.SaveChangesAsync();
        }
        var service = Service(factory, clock);

        Assert.Equal(5, await service.PlanDueAsync(Options()));
        await using var verification = factory.CreateDbContext();
        Assert.Single(await verification.AutomationWorkItems
            .Where(item => item.Type == AutomationWorkType.AutonomousReview)
            .ToListAsync());
    }

    [Fact]
    public async Task PlanDueAsync_AddsCollectionGrowthOnlyForAutomaticPolicy()
    {
        var (factory, clock, shopId) = await CreateDatabaseAsync();
        await using (var context = factory.CreateDbContext())
        {
            context.AutonomousCataloguePolicies.Add(new AutonomousCataloguePolicyRecord
            {
                ShopId = shopId,
                Mode = AutonomousCatalogueMode.Automatic,
                ReviewEveryHours = 1,
                CreatedUtc = clock.UtcNow,
                UpdatedUtc = clock.UtcNow,
                UpdatedBy = "test"
            });
            await context.SaveChangesAsync();
        }
        var service = Service(factory, clock, new AutonomousCatalogueOptions
        {
            Enabled = true,
            AutomaticCollectionGrowthEnabled = true,
            CollectionGrowthEveryHours = 6
        });

        Assert.Equal(6, await service.PlanDueAsync(Options()));
        await using var verification = factory.CreateDbContext();
        var work = await verification.AutomationWorkItems.SingleAsync(
            item => item.Type == AutomationWorkType.CollectionGrowth);
        Assert.Equal(55, work.Priority);
    }

    [Fact]
    public async Task PlanDueAsync_AddsWeeklyCollectionSuggestionWorkWhenEnabled()
    {
        var (factory, clock, _) = await CreateDatabaseAsync();
        var service = Service(factory, clock, new AutonomousCatalogueOptions
        {
            CollectionSuggestionsEnabled = true,
            CollectionSuggestionEveryDays = 7,
            MaximumCollectionSuggestionsPerRun = 3
        });

        Assert.Equal(5, await service.PlanDueAsync(Options()));
        await using var context = factory.CreateDbContext();
        var work = await context.AutomationWorkItems.SingleAsync(item => item.Type == AutomationWorkType.CollectionSuggestions);
        Assert.Equal(40, work.Priority);
    }

    [Fact]
    public async Task ClaimNextAsync_ConcurrentClaimersLeaseOneItemOnce()
    {
        var (factory, clock, shopId) = await CreateDatabaseAsync();
        await SeedWorkAsync(factory, shopId, clock.UtcNow, maximumAttempts: 3);
        var service = Service(factory, clock);

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
        var service = Service(factory, clock);
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
        var service = Service(factory, clock);
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

    [Fact]
    public async Task RenewLeaseAsync_ExtendsOnlyTheCurrentOwnersLease()
    {
        var (factory, clock, shopId) = await CreateDatabaseAsync();
        await SeedWorkAsync(factory, shopId, clock.UtcNow, maximumAttempts: 3);
        var service = Service(factory, clock);
        var lease = await service.ClaimNextAsync("worker-a", TimeSpan.FromMinutes(10));
        clock.UtcNow = clock.UtcNow.AddMinutes(5);

        Assert.False(await service.RenewLeaseAsync(lease!.Id, "worker-b", TimeSpan.FromMinutes(10)));
        Assert.True(await service.RenewLeaseAsync(lease.Id, "worker-a", TimeSpan.FromMinutes(10)));

        await using var context = factory.CreateDbContext();
        var stored = await context.AutomationWorkItems.SingleAsync();
        Assert.Equal(clock.UtcNow.AddMinutes(10), stored.LeaseExpiresUtc);
        Assert.StartsWith("lease-renewed:", stored.Checkpoint);
    }

    [Fact]
    public async Task RequeueDeadLetterAsync_ResetsRetryStateForOperatorRecovery()
    {
        var (factory, clock, shopId) = await CreateDatabaseAsync();
        await SeedWorkAsync(factory, shopId, clock.UtcNow, maximumAttempts: 1);
        var service = Service(factory, clock);
        var lease = await service.ClaimNextAsync("worker-a", TimeSpan.FromMinutes(10));
        Assert.Equal(AutomationWorkStatus.DeadLetter,
            await service.FailAsync(lease!.Id, "worker-a", "test failure", Options()));

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        Assert.True(await service.RequeueDeadLetterAsync(lease.Id));

        await using var context = factory.CreateDbContext();
        var stored = await context.AutomationWorkItems.SingleAsync();
        Assert.Equal(AutomationWorkStatus.Pending, stored.Status);
        Assert.Equal(0, stored.AttemptCount);
        Assert.Equal(clock.UtcNow, stored.AvailableUtc);
        Assert.Null(stored.LastError);
        Assert.Null(stored.CompletedUtc);
        Assert.StartsWith("operator-requeued:", stored.Checkpoint);
    }

    private static CatalogueAutomationOptions Options() => new()
    {
        RefreshEveryHours = 24,
        LinkRefreshHours = 120,
        MaximumAttempts = 3,
        RetryBaseMinutes = 15,
        RetryMaximumMinutes = 60
    };

    private static AutomationWorkQueueService Service(
        InMemoryFactory factory,
        MutableTimeProvider clock,
        AutonomousCatalogueOptions? options = null) =>
        new(factory, clock, options ?? new AutonomousCatalogueOptions());

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
