using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class CatalogueAutomationMonitorServiceTests
{
    [Fact]
    public async Task GetReportAsync_SurfacesFreshnessAvailabilityLinkAndQueueAlerts()
    {
        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        var shopId = Guid.CreateVersion7();
        await using (var context = factory.CreateDbContext())
        {
            context.Shops.Add(Shop(shopId, now));
            context.Products.AddRange(
                Product("published-stale", now, ProductAvailabilityState.Available, true, now.AddHours(-40), null),
                Product("suspected", now, ProductAvailabilityState.SuspectedUnavailable, true, now.AddHours(-2), now),
                Product("unavailable", now, ProductAvailabilityState.Unavailable, false, now.AddHours(-48), now));
            context.ShopProducts.AddRange(
                ShopProduct(shopId, "published-stale"),
                ShopProduct(shopId, "suspected"),
                ShopProduct(shopId, "unavailable"));
            context.AffiliateLinks.AddRange(
                Link(shopId, "published-stale", now, AffiliateLinkStatus.Active, null),
                Link(shopId, "suspected", now, AffiliateLinkStatus.GenerationFailed, now, "generation failed"));
            context.IngestionJobs.Add(new IngestionJobRecord
            {
                Id = Guid.CreateVersion7(),
                ShopId = shopId,
                Type = IngestionJobType.ProductRefresh,
                Status = IngestionJobStatus.Failed,
                QueuedUtc = now.AddHours(-2),
                StartedUtc = now.AddHours(-2),
                CompletedUtc = now.AddHours(-1)
            });
            context.AutomationWorkItems.AddRange(
                new AutomationWorkItemRecord
                {
                    Id = Guid.CreateVersion7(),
                    ShopId = shopId,
                    Type = AutomationWorkType.ProductRefresh,
                    Status = AutomationWorkStatus.DeadLetter,
                    IdempotencyKey = "dead-letter",
                    QueuedUtc = now.AddHours(-3),
                    AvailableUtc = now.AddHours(-3),
                    CompletedUtc = now.AddHours(-2)
                },
                new AutomationWorkItemRecord
                {
                    Id = Guid.CreateVersion7(),
                    ShopId = shopId,
                    Type = AutomationWorkType.LinkRefresh,
                    Status = AutomationWorkStatus.Pending,
                    IdempotencyKey = "delayed",
                    QueuedUtc = now.AddHours(-2),
                    AvailableUtc = now.AddHours(-2)
                });
            await context.SaveChangesAsync();
        }

        var service = new CatalogueAutomationMonitorService(factory, new FixedTimeProvider(now));
        var report = await service.GetReportAsync(new CatalogueAutomationOptions
        {
            RefreshEveryHours = 24,
            ProductStaleAfterHours = 30,
            LinkRefreshHours = 120,
            QueueDelayWarningMinutes = 60,
            FailureAlertHours = 24
        });

        Assert.Equal(1, report.PublishedProducts);
        Assert.Equal(1, report.StaleProducts);
        Assert.Equal(1, report.NeverCheckedProducts);
        Assert.Equal(1, report.SuspectedUnavailableProducts);
        Assert.Equal(1, report.ConfirmedUnavailableProducts);
        Assert.Equal(1, report.StaleLinks);
        Assert.Equal(1, report.FailedLinks);
        Assert.Equal(1, report.FailedRunsInAlertWindow);
        Assert.Equal(1, report.DeadLetters);
        Assert.Contains(report.Alerts, alert => alert.Severity == AutomationAlertSeverity.Critical && alert.Title.Contains("Dead-letter", StringComparison.Ordinal));
        Assert.Contains(report.Alerts, alert => alert.Title.Contains("stale", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Alerts, alert => alert.Title.Contains("Availability", StringComparison.OrdinalIgnoreCase));
    }

    private static ShopRecord Shop(Guid id, DateTimeOffset now) => new()
    {
        Id = id,
        Slug = "plushies",
        DisplayName = "The Plushy Shop",
        PathPrefix = "/plushies",
        TrackingId = "theplushyshop",
        DefaultSearchQuery = "plush toy",
        SeoTitle = "Plush toys",
        SeoDescription = "Curated plush toys",
        PrimaryColour = "#000000",
        AccentColour = "#ffffff",
        CreatedUtc = now,
        UpdatedUtc = now
    };

    private static ProductRecord Product(
        string id,
        DateTimeOffset now,
        ProductAvailabilityState state,
        bool eligible,
        DateTimeOffset? detailRefreshedUtc,
        DateTimeOffset? checkedUtc) => new()
    {
        AliExpressProductId = id,
        Title = id,
        IsEligible = eligible,
        AvailabilityState = state,
        FirstSeenUtc = now.AddDays(-10),
        LastSeenUtc = now,
        LastRefreshedUtc = now,
        LastDetailRefreshedUtc = detailRefreshedUtc,
        LastCheckedUtc = checkedUtc
    };

    private static ShopProductRecord ShopProduct(Guid shopId, string productId) => new()
    {
        ShopId = shopId,
        ProductId = productId,
        IsActive = true,
        ReviewStatus = ProductReviewStatus.Approved
    };

    private static AffiliateLinkRecord Link(
        Guid shopId,
        string productId,
        DateTimeOffset now,
        AffiliateLinkStatus status,
        DateTimeOffset? lastValidatedUtc,
        string? error = null) => new()
    {
        Id = Guid.CreateVersion7(),
        ShopId = shopId,
        ProductId = productId,
        SourceUrl = $"https://example.test/item/{productId}",
        PromotionUrl = $"https://example.test/go/{productId}",
        TrackingId = "theplushyshop",
        Status = status,
        GeneratedUtc = now.AddDays(-10),
        LastValidatedUtc = lastValidatedUtc,
        LastError = error
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
