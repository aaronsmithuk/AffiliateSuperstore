using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class CatalogueProductEnrichmentServiceTests
{
    [Fact]
    public async Task RunAsync_StoresDetailFactsGallerySnapshotAndCompletedJob()
    {
        var factory = await CreateDatabaseAsync();
        var source = new FakeDetailSource
        {
            Products =
            [
                Product("1001") with
                {
                    SkuId = "sku-1001",
                    EanCode = "5012345678900",
                    SmallImageUrls =
                    [
                        "https://ae01.alicdn.com/kf/1001.jpg",
                        "https://ae01.alicdn.com/kf/1001-side.jpg"
                    ],
                    VideoUrl = "https://video.aliexpress-media.com/1001.mp4"
                }
            ]
        };
        var service = CreateService(source, factory);

        var result = await service.RunAsync("plushies", force: true);

        Assert.Equal(IngestionJobStatus.Succeeded, result.Status);
        Assert.Equal(1, result.ProductsEnriched);
        Assert.Equal(3, result.MediaItemsStored);
        Assert.Single(source.Requests);
        Assert.Equal("1001", Assert.Single(source.Requests[0]));
        await using var context = factory.CreateDbContext();
        var product = await context.Products.SingleAsync();
        Assert.Equal("sku-1001", product.SkuId);
        Assert.Equal("5012345678900", product.EanCode);
        Assert.NotNull(product.LastDetailRefreshedUtc);
        Assert.Equal(2, await context.ProductMedia.CountAsync(item => item.Type == ProductMediaType.Image));
        Assert.Equal(1, await context.ProductMedia.CountAsync(item => item.Type == ProductMediaType.Video));
        Assert.Equal(9.49m, (await context.ProductSnapshots.SingleAsync()).SalePrice);
        Assert.Equal(IngestionJobStatus.Succeeded, (await context.IngestionJobs.SingleAsync()).Status);
    }

    [Fact]
    public async Task RunAsync_SkipsRecentlyEnrichedProductUnlessForced()
    {
        var factory = await CreateDatabaseAsync(recentlyRefreshed: true);
        var source = new FakeDetailSource { Products = [Product("1001")] };

        var result = await CreateService(source, factory).RunAsync("plushies");

        Assert.Equal(IngestionJobStatus.Succeeded, result.Status);
        Assert.Equal(0, result.ProductsSelected);
        Assert.Empty(source.Requests);
    }

    [Fact]
    public async Task RunAsync_DeduplicatesUnchangedObservationsAndTracksSafeAvailabilityLifecycle()
    {
        var factory = await CreateDatabaseAsync();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 30, 8, 0, 0, TimeSpan.Zero));
        var source = new FakeDetailSource { Products = [Product("1001")] };
        var service = CreateService(source, factory, clock);

        var first = await service.RunAsync("plushies", force: true);
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var unchanged = await service.RunAsync("plushies", force: true);

        Assert.Equal(1, first.ProductsChanged);
        Assert.Equal(0, unchanged.ProductsChanged);
        await using (var context = factory.CreateDbContext())
        {
            Assert.Equal(1, await context.ProductSnapshots.CountAsync());
            Assert.Equal(1, await context.ProductChangeEvents.CountAsync(item => item.Kind == ProductChangeEventKind.ObservationCreated));
            Assert.Equal(clock.UtcNow, (await context.Products.SingleAsync()).LastCheckedUtc);
        }

        source.Products = [];
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var firstMiss = await service.RunAsync("plushies", force: true);
        clock.UtcNow = clock.UtcNow.AddHours(7);
        var earlySecondMiss = await service.RunAsync("plushies", force: true);

        Assert.Equal(1, firstMiss.ProductsSuspectedUnavailable);
        Assert.Equal(0, earlySecondMiss.ProductsConfirmedUnavailable);
        await using (var context = factory.CreateDbContext())
        {
            var product = await context.Products.SingleAsync();
            Assert.True(product.IsEligible);
            Assert.Equal(ProductAvailabilityState.SuspectedUnavailable, product.AvailabilityState);
            Assert.Equal(2, product.ConsecutiveUnavailableChecks);
        }

        clock.UtcNow = clock.UtcNow.AddHours(18);
        var confirmed = await service.RunAsync("plushies", force: true);

        Assert.Equal(1, confirmed.ProductsConfirmedUnavailable);
        await using (var context = factory.CreateDbContext())
        {
            var product = await context.Products.SingleAsync();
            Assert.False(product.IsEligible);
            Assert.Equal(ProductAvailabilityState.Unavailable, product.AvailabilityState);
            Assert.Equal("availability:confirmed-unavailable", product.IneligibilityReason);
        }

        source.Products = [Product("1001")];
        clock.UtcNow = clock.UtcNow.AddDays(1);
        var restored = await service.RunAsync("plushies", force: true);

        Assert.Equal(1, restored.ProductsRestored);
        await using (var context = factory.CreateDbContext())
        {
            var product = await context.Products.SingleAsync();
            Assert.True(product.IsEligible);
            Assert.Equal(ProductAvailabilityState.Available, product.AvailabilityState);
            Assert.Equal(0, product.ConsecutiveUnavailableChecks);
            Assert.Null(product.IneligibilityReason);
            Assert.Equal(3, await context.ProductChangeEvents.CountAsync(item => item.Kind == ProductChangeEventKind.AvailabilityChanged));
        }
    }

    private static CatalogueProductEnrichmentService CreateService(
        IAffiliateProductDetailSource source,
        IDbContextFactory<AffiliateSuperstoreDbContext> factory,
        TimeProvider? timeProvider = null)
    {
        var clock = timeProvider ?? TimeProvider.System;
        return new(source, factory, clock, new ProductQualityAssessmentService(factory, clock));
    }

    private static async Task<InMemoryFactory> CreateDatabaseAsync(bool recentlyRefreshed = false)
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await using var context = factory.CreateDbContext();
        var shopId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
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
            CreatedUtc = now,
            UpdatedUtc = now
        });
        context.Products.Add(new ProductRecord
        {
            AliExpressProductId = "1001",
            Title = "Original title",
            ProductDetailUrl = "https://www.aliexpress.com/item/1001.html",
            MainImageUrl = "https://ae01.alicdn.com/kf/1001.jpg",
            IsEligible = true,
            FirstSeenUtc = now,
            LastSeenUtc = now,
            LastRefreshedUtc = now,
            LastDetailRefreshedUtc = recentlyRefreshed ? now : null
        });
        context.ShopProducts.Add(new ShopProductRecord
        {
            ShopId = shopId,
            ProductId = "1001",
            IsActive = true,
            ReviewStatus = ProductReviewStatus.Approved,
            FirstIncludedUtc = now,
            LastIncludedUtc = now
        });
        context.AffiliateLinks.Add(new AffiliateLinkRecord
        {
            Id = Guid.CreateVersion7(),
            ShopId = shopId,
            ProductId = "1001",
            SourceUrl = "https://www.aliexpress.com/item/1001.html",
            PromotionUrl = "https://s.click.aliexpress.com/e/1001",
            TrackingId = "theplushyshop",
            Status = AffiliateLinkStatus.Active,
            GeneratedUtc = now,
            LastValidatedUtc = now
        });
        await context.SaveChangesAsync();
        return factory;
    }

    private static AliExpressProduct Product(string id) => new(
        id,
        null,
        "Detailed green plush dragon",
        $"https://ae01.alicdn.com/kf/{id}.jpg",
        $"https://www.aliexpress.com/item/{id}.html",
        null,
        "9.49",
        "12.99",
        "GBP",
        "7%",
        null,
        "27%",
        "98.6%",
        436,
        "1",
        "Toys",
        "2",
        "Plush",
        "seller-1",
        "Plush Store",
        "https://www.aliexpress.com/store/1",
        "20%");

    private sealed class FakeDetailSource : IAffiliateProductDetailSource
    {
        public IReadOnlyList<AliExpressProduct> Products { get; set; } = [];
        public List<IReadOnlyCollection<string>> Requests { get; } = [];

        public Task<IReadOnlyList<AliExpressProduct>> GetDetailsAsync(
            IReadOnlyCollection<string> productIds,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(productIds.ToArray());
            return Task.FromResult<IReadOnlyList<AliExpressProduct>>(
                Products.Where(item => productIds.Contains(item.ProductId)).ToArray());
        }
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
