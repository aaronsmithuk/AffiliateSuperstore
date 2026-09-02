using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AdvancedCatalogueDiscoveryPreviewServiceTests
{
    [Fact]
    public async Task PreviewHotProducts_AssessesEligibilityQualityAndExistingCatalogueWithoutWrites()
    {
        var factory = await CreateDatabaseAsync();
        var source = new FakeSource
        {
            Products =
            [
                Product("existing", "Existing otter plush"),
                Product("clear", "Soft highland cow plush"),
                Product("flagged", "Mimikyu anime character plush"),
                Product("usd", "USD capybara plush") with { Currency = "USD" }
            ]
        };
        var quality = new ProductQualityAssessmentService(factory, TimeProvider.System);
        var service = new AdvancedCatalogueDiscoveryPreviewService(source, factory, quality);

        var result = await service.PreviewAsync(
            "plushies", CatalogueDiscoverySource.HotProductQuery, "plush toy", pageSize: 10);

        Assert.Equal(4, result.ProductsRead);
        Assert.Equal(3, result.MinimallyEligible);
        Assert.Equal(1, result.AlreadyInCatalogue);
        Assert.Equal(1, result.QualityClearNewCandidates);
        Assert.Equal("Already in catalogue", result.Candidates.Single(item => item.ProductId == "existing").RecommendedAction);
        Assert.Contains(
            result.Candidates.Single(item => item.ProductId == "flagged").QualityFlags,
            flag => flag.Code == "ip.third-party-character");
        Assert.Equal("Reject: incomplete or non-GBP result", result.Candidates.Single(item => item.ProductId == "usd").RecommendedAction);
        Assert.Equal(1, source.HotProductRequests);
        Assert.Equal(0, source.GenerateLinkRequests);

        await using var context = factory.CreateDbContext();
        Assert.Equal(1, await context.Products.CountAsync());
        Assert.Equal(1, await context.AffiliateLinks.CountAsync());
        Assert.Empty(await context.IngestionJobs.ToArrayAsync());
    }

    [Fact]
    public async Task PreviewSmartMatch_PassesOnlyBackendSeedInputsToSource()
    {
        var factory = await CreateDatabaseAsync();
        var source = new FakeSource { Products = [Product("suggested", "Suggested frog plush")] };
        var service = new AdvancedCatalogueDiscoveryPreviewService(
            source,
            factory,
            new ProductQualityAssessmentService(factory, TimeProvider.System));

        await service.PreviewAsync(
            "plushies",
            CatalogueDiscoverySource.SmartMatch,
            "frog plush",
            "approved-product",
            10);

        Assert.Equal(("approved-product", "frog plush", 1), source.SmartMatchRequest);
        Assert.Equal(0, source.GenerateLinkRequests);
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
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        });
        context.Products.Add(new ProductRecord
        {
            AliExpressProductId = "existing",
            Title = "Existing otter plush",
            FirstSeenUtc = DateTimeOffset.UtcNow,
            LastSeenUtc = DateTimeOffset.UtcNow,
            LastRefreshedUtc = DateTimeOffset.UtcNow
        });
        context.AffiliateLinks.Add(new AffiliateLinkRecord
        {
            Id = Guid.CreateVersion7(),
            ShopId = shopId,
            ProductId = "existing",
            SourceUrl = "https://www.aliexpress.com/item/existing.html",
            PromotionUrl = "https://s.click.aliexpress.com/e/existing",
            TrackingId = "theplushyshop",
            PromotionLinkType = 2,
            Status = AffiliateLinkStatus.Active,
            GeneratedUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        return factory;
    }

    private static AliExpressProduct Product(string id, string title) => new(
        id,
        null,
        title,
        $"https://ae01.alicdn.com/kf/{id}.jpg",
        $"https://www.aliexpress.com/item/{id}.html",
        null,
        "9.99",
        "12.99",
        "GBP",
        "7%",
        "12%",
        "20%",
        "98%",
        100,
        "1",
        "Toys",
        "2",
        "Plush",
        "seller",
        "Plush Store",
        null,
        null);

    private sealed class FakeSource : IAffiliateCatalogueSource
    {
        public IReadOnlyList<AliExpressProduct> Products { get; init; } = [];
        public int HotProductRequests { get; private set; }
        public int GenerateLinkRequests { get; private set; }
        public (string? ProductId, string? Keywords, int Page)? SmartMatchRequest { get; private set; }

        public Task<AliExpressPage<AliExpressProduct>> SearchAsync(string keywords, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AliExpressPage<AliExpressProduct>> SearchHotProductsAsync(string keywords, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            HotProductRequests++;
            return Task.FromResult(new AliExpressPage<AliExpressProduct>(Products, pageNumber, 1, Products.Count));
        }

        public Task<AliExpressPage<AliExpressProduct>> SmartMatchAsync(string? productId, string? keywords, int pageNumber, CancellationToken cancellationToken = default)
        {
            SmartMatchRequest = (productId, keywords, pageNumber);
            return Task.FromResult(new AliExpressPage<AliExpressProduct>(Products, pageNumber, 1, Products.Count));
        }

        public Task<IReadOnlyList<AliExpressPromotionLink>> GenerateLinksAsync(IReadOnlyCollection<string> sourceUrls, string trackingId, CancellationToken cancellationToken = default)
        {
            GenerateLinkRequests++;
            return Task.FromResult<IReadOnlyList<AliExpressPromotionLink>>([]);
        }
    }

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }
}
