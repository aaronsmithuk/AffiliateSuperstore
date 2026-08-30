using AffiliateSuperstore.Application.Tracking;
using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Core.Tracking;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class OutboundRedirectServiceTests
{
    [Fact]
    public async Task CreateAsync_ApprovedProduct_PersistsAttributedClickAndReturnsTrackedUrl()
    {
        var factory = await CreateDatabaseAsync(ProductReviewStatus.Approved);
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 30, 19, 0, 0, TimeSpan.Zero));
        var tracking = new AffiliateTrackingService(new FixedClickIdGenerator(), new AffiliateSuperstoreOptions());
        var service = new OutboundRedirectService(factory, tracking, clock);

        var result = await service.CreateAsync("plushies", "1001", "basket", "anonymous-hash");

        Assert.NotNull(result);
        Assert.Contains("cn=plushies", result.Url, StringComparison.Ordinal);
        Assert.Contains("cv=basket", result.Url, StringComparison.Ordinal);
        Assert.Contains("dp=fixed-click-id", result.Url, StringComparison.Ordinal);
        await using var context = factory.CreateDbContext();
        var click = await context.OutboundClicks.SingleAsync();
        Assert.Equal("theplushyshop", click.TrackingId);
        Assert.Equal("basket", click.Placement);
        Assert.Equal("anonymous-hash", click.AnonymousSessionHash);
        Assert.Equal(clock.GetUtcNow(), click.ClickedUtc);
    }

    [Theory]
    [InlineData(ProductReviewStatus.Pending)]
    [InlineData(ProductReviewStatus.NeedsReview)]
    [InlineData(ProductReviewStatus.Rejected)]
    public async Task CreateAsync_UnapprovedProduct_ReturnsNoRedirectOrClick(ProductReviewStatus status)
    {
        var factory = await CreateDatabaseAsync(status);
        var tracking = new AffiliateTrackingService(new FixedClickIdGenerator(), new AffiliateSuperstoreOptions());
        var service = new OutboundRedirectService(factory, tracking, TimeProvider.System);

        var result = await service.CreateAsync("plushies", "1001", "product-card");

        Assert.Null(result);
        await using var context = factory.CreateDbContext();
        Assert.Empty(await context.OutboundClicks.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_UnknownPlacement_FallsBackToProductCard()
    {
        var factory = await CreateDatabaseAsync(ProductReviewStatus.Approved);
        var tracking = new AffiliateTrackingService(new FixedClickIdGenerator(), new AffiliateSuperstoreOptions());
        var service = new OutboundRedirectService(factory, tracking, TimeProvider.System);

        var result = await service.CreateAsync("plushies", "1001", "untrusted-value");

        Assert.NotNull(result);
        Assert.Contains("cv=product-card", result.Url, StringComparison.Ordinal);
    }

    private static async Task<InMemoryFactory> CreateDatabaseAsync(ProductReviewStatus status)
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await using var context = factory.CreateDbContext();
        var shopId = Guid.CreateVersion7();
        var linkId = Guid.CreateVersion7();
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
            PrimaryColour = "#6f4bc3",
            AccentColour = "#f3c8ef",
            IsEnabled = true,
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        });
        context.Products.Add(new ProductRecord
        {
            AliExpressProductId = "1001",
            Title = "Green plush dragon",
            ProductDetailUrl = "https://www.aliexpress.com/item/1001.html",
            MainImageUrl = "https://example.com/dragon.jpg",
            IsEligible = true,
            FirstSeenUtc = DateTimeOffset.UtcNow,
            LastSeenUtc = DateTimeOffset.UtcNow,
            LastRefreshedUtc = DateTimeOffset.UtcNow
        });
        context.ShopProducts.Add(new ShopProductRecord
        {
            ShopId = shopId,
            ProductId = "1001",
            IsActive = true,
            ReviewStatus = status,
            FirstIncludedUtc = DateTimeOffset.UtcNow,
            LastIncludedUtc = DateTimeOffset.UtcNow
        });
        context.AffiliateLinks.Add(new AffiliateLinkRecord
        {
            Id = linkId,
            ShopId = shopId,
            ProductId = "1001",
            SourceUrl = "https://www.aliexpress.com/item/1001.html",
            PromotionUrl = "https://s.click.aliexpress.com/e/example",
            TrackingId = "theplushyshop",
            PromotionLinkType = 0,
            Status = AffiliateLinkStatus.Active,
            GeneratedUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        return factory;
    }

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }

    private sealed class FixedClickIdGenerator : IClickIdGenerator
    {
        public string Create() => "fixed-click-id";
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
