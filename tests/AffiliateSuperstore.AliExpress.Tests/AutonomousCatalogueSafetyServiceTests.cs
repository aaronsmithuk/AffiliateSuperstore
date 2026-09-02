using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AutonomousCatalogueSafetyServiceTests
{
    [Fact]
    public async Task EnsureSafeAsync_ThreeConsecutiveAiFailures_DowngradesAutomaticToShadow()
    {
        var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, now, now.AddHours(-2));
        await using (var context = factory.CreateDbContext())
        {
            for (var index = 0; index < 3; index++)
            {
                context.AiInvocations.Add(Invocation(now.AddMinutes(-index - 1), AiInvocationStatus.Failed, $"failed-{index}"));
            }
            await context.SaveChangesAsync();
        }

        var policy = await PolicyAsync(factory, now);
        var result = await Service(factory, now).EnsureSafeAsync(policy);

        Assert.True(result.Blocked);
        Assert.Equal(AutonomousCatalogueMode.Shadow, result.EffectiveMode);
        Assert.Equal("ai.consecutive-failures", result.ReasonCode);
        await using var verification = factory.CreateDbContext();
        var row = await verification.AutonomousCataloguePolicies.SingleAsync();
        Assert.Equal(AutonomousCatalogueMode.Shadow, row.Mode);
        Assert.Equal("automatic safety: ai.consecutive-failures", row.UpdatedBy);
    }

    [Fact]
    public async Task EnsureSafeAsync_RecentAutonomousDeadLetter_DowngradesAutomaticToShadow()
    {
        var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        var shopId = await SeedAsync(factory, now, now.AddHours(-2));
        await using (var context = factory.CreateDbContext())
        {
            context.AutomationWorkItems.Add(new AutomationWorkItemRecord
            {
                Id = Guid.CreateVersion7(),
                ShopId = shopId,
                Type = AutomationWorkType.AutonomousReview,
                Status = AutomationWorkStatus.DeadLetter,
                IdempotencyKey = "dead-letter-test",
                Priority = 50,
                QueuedUtc = now.AddMinutes(-20),
                AvailableUtc = now.AddMinutes(-20),
                AttemptCount = 5,
                MaximumAttempts = 5,
                CompletedUtc = now.AddMinutes(-1)
            });
            await context.SaveChangesAsync();
        }

        var result = await Service(factory, now).EnsureSafeAsync(await PolicyAsync(factory, now));

        Assert.True(result.Blocked);
        Assert.Equal("autonomous.dead-letter", result.ReasonCode);
        await using var verification = factory.CreateDbContext();
        Assert.Equal(AutonomousCatalogueMode.Shadow, (await verification.AutonomousCataloguePolicies.SingleAsync()).Mode);
    }

    [Fact]
    public async Task EnsureSafeAsync_FailuresBeforeLatestOwnerUpdate_AreAcknowledged()
    {
        var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, now, now.AddMinutes(-10));
        await using (var context = factory.CreateDbContext())
        {
            for (var index = 0; index < 3; index++)
            {
                context.AiInvocations.Add(Invocation(now.AddHours(-1).AddMinutes(-index), AiInvocationStatus.Failed, $"old-{index}"));
            }
            await context.SaveChangesAsync();
        }

        var result = await Service(factory, now).EnsureSafeAsync(await PolicyAsync(factory, now));

        Assert.False(result.Blocked);
        await using var verification = factory.CreateDbContext();
        Assert.Equal(AutonomousCatalogueMode.Automatic, (await verification.AutonomousCataloguePolicies.SingleAsync()).Mode);
    }

    private static AutonomousCatalogueSafetyService Service(InMemoryFactory factory, DateTimeOffset now) =>
        new(factory, new AutonomousCatalogueOptions
        {
            AutomaticSafetyCircuitEnabled = true,
            AutomaticPauseLookbackHours = 24,
            AutomaticPauseConsecutiveFailedAiCalls = 3
        }, new FixedTimeProvider(now));

    private static async Task<AutonomousCataloguePolicy> PolicyAsync(InMemoryFactory factory, DateTimeOffset now) =>
        (await new AutonomousCataloguePolicyService(
            factory,
            new AutonomousCatalogueOptions { Enabled = true, AutomaticPublishingEnabled = true },
            new FixedTimeProvider(now)).GetAsync("plushies"))!;

    private static async Task<Guid> SeedAsync(
        InMemoryFactory factory,
        DateTimeOffset now,
        DateTimeOffset policyUpdatedUtc)
    {
        await using var context = factory.CreateDbContext();
        var shopId = Guid.CreateVersion7();
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
            CreatedUtc = now.AddDays(-1),
            UpdatedUtc = now.AddDays(-1)
        });
        context.AutonomousCataloguePolicies.Add(new AutonomousCataloguePolicyRecord
        {
            ShopId = shopId,
            Mode = AutonomousCatalogueMode.Automatic,
            ReviewEveryHours = 1,
            MaximumCandidatesPerRun = 2,
            MaximumAutoPublishesPerDay = 2,
            MinimumReadinessScore = 1m,
            DuplicateHoldConfidence = .75m,
            DailyAiBudgetUsd = .25m,
            CreatedUtc = now.AddDays(-1),
            UpdatedUtc = policyUpdatedUtc,
            UpdatedBy = "owner"
        });
        context.Products.Add(new ProductRecord
        {
            AliExpressProductId = "product-1",
            Title = "Plush toy",
            ProductDetailUrl = "https://example.test/product-1",
            IsEligible = true,
            FirstSeenUtc = now.AddDays(-1),
            LastSeenUtc = now,
            LastRefreshedUtc = now,
            LastCheckedUtc = now,
            AvailabilityState = ProductAvailabilityState.Available
        });
        context.ShopProducts.Add(new ShopProductRecord
        {
            ShopId = shopId,
            ProductId = "product-1",
            ReviewStatus = ProductReviewStatus.Pending,
            FirstIncludedUtc = now,
            LastIncludedUtc = now
        });
        await context.SaveChangesAsync();
        return shopId;
    }

    private static AiInvocationRecord Invocation(DateTimeOffset requestedUtc, AiInvocationStatus status, string key) => new()
    {
        Id = Guid.CreateVersion7(),
        Purpose = AiInvocationAuditService.ProductCopyPurpose,
        ProductId = "product-1",
        Provider = "OpenAI",
        Model = "test-model",
        PromptVersion = CatalogueAiSuggestionService.PromptVersion,
        InputHash = key,
        CacheKey = key,
        Status = status,
        RequestedUtc = requestedUtc,
        CompletedUtc = requestedUtc.AddSeconds(1),
        EditorialValidationState = EditorialValidationState.NotEvaluated
    };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }
}
