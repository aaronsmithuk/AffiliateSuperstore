using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class CatalogueIngestionServiceTests
{
    [Fact]
    public async Task RunAsync_WritesEligibleProductSnapshotLinkAndCompletedJob()
    {
        var factory = await CreateDatabaseAsync();
        var source = new FakeSource
        {
            Products = [Product("1001", "Green plush dragon", "8.99", "GBP")]
        };
        var service = CreateService(source, factory);

        var result = await service.RunAsync(new CatalogueIngestionRequest("plushies", PageSize: 10));

        Assert.Equal(IngestionJobStatus.Succeeded, result.Status);
        Assert.Equal(1, result.ProductsWritten);
        Assert.Equal(1, result.LinksCreatedOrRefreshed);
        await using var context = factory.CreateDbContext();
        Assert.Equal("Green plush dragon", (await context.Products.SingleAsync()).Title);
        Assert.Equal(8.99m, (await context.ProductSnapshots.SingleAsync()).SalePrice);
        Assert.Equal(.07m, (await context.ProductSnapshots.SingleAsync()).CommissionRate);
        Assert.Equal("theplushyshop", (await context.AffiliateLinks.SingleAsync()).TrackingId);
        Assert.Equal(IngestionJobStatus.Succeeded, (await context.IngestionJobs.SingleAsync()).Status);
    }

    [Fact]
    public async Task RunAsync_RejectsProductWithoutRequiredImageAndGbpPrice()
    {
        var factory = await CreateDatabaseAsync();
        var source = new FakeSource
        {
            Products =
            [
                Product("1001", "Eligible", "8.99", "GBP"),
                Product("1002", "No image", "4.99", "GBP") with { MainImageUrl = null },
                Product("1003", "Wrong currency", "4.99", "USD")
            ]
        };
        var service = CreateService(source, factory);

        var result = await service.RunAsync(new CatalogueIngestionRequest("plushies"));

        Assert.Equal(IngestionJobStatus.PartiallySucceeded, result.Status);
        Assert.Equal(3, result.ProductsRead);
        Assert.Equal(1, result.ProductsWritten);
        Assert.Equal(2, result.ProductsRejected);
        await using var context = factory.CreateDbContext();
        Assert.Equal(1, await context.Products.CountAsync());
    }

    [Fact]
    public async Task RunAsync_RecordsSourceFailureInJob()
    {
        var factory = await CreateDatabaseAsync();
        var service = CreateService(new ThrowingSource(), factory);

        var result = await service.RunAsync(new CatalogueIngestionRequest("plushies"));

        Assert.Equal(IngestionJobStatus.Failed, result.Status);
        Assert.Contains("simulated outage", result.Error, StringComparison.Ordinal);
        await using var context = factory.CreateDbContext();
        var job = await context.IngestionJobs.SingleAsync();
        Assert.Equal(IngestionJobStatus.Failed, job.Status);
        Assert.Contains("simulated outage", job.ErrorSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_FlagsRiskyImportedProductForReview()
    {
        var factory = await CreateDatabaseAsync();
        var source = new FakeSource
        {
            Products = [Product("1004", "Mimikyu anime character plush doll", "8.99", "GBP")]
        };

        var result = await CreateService(source, factory)
            .RunAsync(new CatalogueIngestionRequest("plushies"));

        Assert.Equal(IngestionJobStatus.Succeeded, result.Status);
        await using var context = factory.CreateDbContext();
        var shopProduct = await context.ShopProducts.SingleAsync();
        Assert.Equal(ProductReviewStatus.NeedsReview, shopProduct.ReviewStatus);
        Assert.Contains("ip.third-party-character", shopProduct.AutomatedReviewFlags, StringComparison.Ordinal);
        Assert.NotNull(shopProduct.AutomatedReviewedUtc);
    }

    [Fact]
    public async Task RunAsync_DiscoveryMissDoesNotCreateAvailabilityEvidence()
    {
        var factory = await CreateDatabaseAsync();
        var source = new FakeSource { Products = [Product("1001", "Green plush dragon", "8.99", "GBP")] };
        var service = CreateService(source, factory);
        await service.RunAsync(new CatalogueIngestionRequest("plushies"));

        source.Products = [];
        await service.RunAsync(new CatalogueIngestionRequest("plushies"));

        await using var context = factory.CreateDbContext();
        var product = await context.Products.SingleAsync();
        Assert.Equal(ProductAvailabilityState.Available, product.AvailabilityState);
        Assert.Equal(0, product.ConsecutiveUnavailableChecks);
        Assert.Empty(await context.ProductChangeEvents
            .Where(item => item.Kind == ProductChangeEventKind.UnavailableEvidence)
            .ToListAsync());
    }

    private static CatalogueIngestionService CreateService(
        IAffiliateCatalogueSource source,
        IDbContextFactory<AffiliateSuperstoreDbContext> factory) =>
        new(source, factory, TimeProvider.System, new ProductQualityAssessmentService(factory, TimeProvider.System));

    private static async Task<InMemoryFactory> CreateDatabaseAsync()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await using var context = factory.CreateDbContext();
        context.Shops.Add(new ShopRecord
        {
            Id = Guid.CreateVersion7(),
            Slug = "plushies",
            DisplayName = "The Plushy Shop",
            PathPrefix = "/plushies",
            TrackingId = "theplushyshop",
            DefaultSearchQuery = "plush toy",
            SeoTitle = "Plush toys",
            SeoDescription = "Curated plush toys",
            PrimaryColour = "#000000",
            AccentColour = "#ffffff",
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        return factory;
    }

    private static AliExpressProduct Product(string id, string title, string price, string currency) => new(
        id,
        null,
        title,
        $"https://ae01.alicdn.com/kf/{id}.jpg",
        $"https://www.aliexpress.com/item/{id}.html",
        null,
        price,
        "12.99",
        currency,
        "7%",
        null,
        "30%",
        "98%",
        100,
        "1",
        "Toys",
        "2",
        "Plush",
        "seller-1",
        "Plush Store",
        "https://www.aliexpress.com/store/1",
        null);

    private sealed class FakeSource : IAffiliateCatalogueSource
    {
        public IReadOnlyList<AliExpressProduct> Products { get; set; } = [];

        public Task<AliExpressPage<AliExpressProduct>> SearchAsync(string keywords, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AliExpressPage<AliExpressProduct>(Products, pageNumber, 1, Products.Count));

        public Task<IReadOnlyList<AliExpressPromotionLink>> GenerateLinksAsync(IReadOnlyCollection<string> sourceUrls, string trackingId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AliExpressPromotionLink>>(
                sourceUrls.Select(url => new AliExpressPromotionLink(url, $"https://s.click.aliexpress.com/e/{Math.Abs(url.GetHashCode())}", null)).ToArray());
    }

    private sealed class ThrowingSource : IAffiliateCatalogueSource
    {
        public Task<AliExpressPage<AliExpressProduct>> SearchAsync(string keywords, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("simulated outage");

        public Task<IReadOnlyList<AliExpressPromotionLink>> GenerateLinksAsync(IReadOnlyCollection<string> sourceUrls, string trackingId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }
}
