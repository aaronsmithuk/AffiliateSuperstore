using AffiliateSuperstore.Application.Reporting;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AffiliateImpressionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordAsync_AggregatesEligibleVisiblePlacementsWithoutVisitorRecords()
    {
        var factory = await CreateDatabaseAsync();
        var timeProvider = new MutableTimeProvider(Now);
        var service = new AffiliateImpressionService(factory, timeProvider);
        var impressions = new[]
        {
            new AffiliateImpressionInput("plushies", "approved", AffiliateImpressionPlacements.ProductCard),
            new AffiliateImpressionInput("plushies", "approved", AffiliateImpressionPlacements.ProductCard),
            new AffiliateImpressionInput("plushies", "pending", AffiliateImpressionPlacements.ProductCard),
            new AffiliateImpressionInput("plushies", "unlinked", AffiliateImpressionPlacements.ProductCard),
            new AffiliateImpressionInput("plushies", "approved", "unknown-placement"),
            new AffiliateImpressionInput("", "approved", AffiliateImpressionPlacements.ProductCard)
        };

        var first = await service.RecordAsync(impressions);
        var second = await service.RecordAsync(impressions);
        timeProvider.UtcNow = Now.AddDays(1);
        var nextDay = await service.RecordAsync([
            new AffiliateImpressionInput("plushies", "approved", AffiliateImpressionPlacements.ProductPage)
        ]);

        Assert.Equal(new AffiliateImpressionResult(6, 1, 5), first);
        Assert.Equal(new AffiliateImpressionResult(6, 1, 5), second);
        Assert.Equal(new AffiliateImpressionResult(1, 1, 0), nextDay);
        await using var context = factory.CreateDbContext();
        var aggregates = await context.ProductImpressions.OrderBy(item => item.DateUtc).ToArrayAsync();
        Assert.Equal(2, aggregates.Length);
        Assert.Equal(2, aggregates[0].Count);
        Assert.Equal(AffiliateImpressionPlacements.ProductCard, aggregates[0].Placement);
        Assert.Equal(1, aggregates[1].Count);
        Assert.Equal(AffiliateImpressionPlacements.ProductPage, aggregates[1].Placement);
    }

    [Fact]
    public async Task RecordAsync_RejectsOversizedBatch()
    {
        var factory = await CreateDatabaseAsync();
        var service = new AffiliateImpressionService(factory, new MutableTimeProvider(Now));
        var impressions = Enumerable.Range(0, AffiliateImpressionService.MaximumBatchSize + 1)
            .Select(index => new AffiliateImpressionInput("plushies", $"product-{index}", AffiliateImpressionPlacements.ProductCard))
            .ToArray();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.RecordAsync(impressions));
    }

    private static async Task<InMemoryFactory> CreateDatabaseAsync()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
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
            IsEnabled = true,
            CreatedUtc = Now,
            UpdatedUtc = Now
        });
        context.Products.AddRange(Product("approved"), Product("pending"), Product("unlinked"));
        context.ShopProducts.AddRange(
            ShopProduct(shopId, "approved", ProductReviewStatus.Approved),
            ShopProduct(shopId, "pending", ProductReviewStatus.Pending),
            ShopProduct(shopId, "unlinked", ProductReviewStatus.Approved));
        context.AffiliateLinks.AddRange(
            Link(shopId, "approved"),
            Link(shopId, "pending"));
        await context.SaveChangesAsync();
        return factory;
    }

    private static ProductRecord Product(string id) => new()
    {
        AliExpressProductId = id,
        Title = $"Product {id}",
        IsEligible = true,
        FirstSeenUtc = Now,
        LastSeenUtc = Now,
        LastRefreshedUtc = Now
    };

    private static ShopProductRecord ShopProduct(Guid shopId, string productId, ProductReviewStatus status) => new()
    {
        ShopId = shopId,
        ProductId = productId,
        IsActive = true,
        ReviewStatus = status,
        FirstIncludedUtc = Now,
        LastIncludedUtc = Now
    };

    private static AffiliateLinkRecord Link(Guid shopId, string productId) => new()
    {
        Id = Guid.CreateVersion7(),
        ShopId = shopId,
        ProductId = productId,
        SourceUrl = $"https://www.aliexpress.com/item/{productId}.html",
        PromotionUrl = $"https://s.click.aliexpress.com/e/{productId}",
        TrackingId = "theplushyshop",
        Status = AffiliateLinkStatus.Active,
        GeneratedUtc = Now
    };

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
