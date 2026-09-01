using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class CatalogueCollectionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SeedRecommendedAsync_CreatesEightUnpublishedBrandSafeDrafts()
    {
        var factory = await CreateDatabaseAsync();
        var service = CreateService(factory);

        var result = await service.SeedRecommendedAsync("plushies", "owner@example.test");
        var collections = await service.GetCollectionsAsync("plushies");

        Assert.True(result.Succeeded);
        Assert.Equal(8, collections.Count);
        Assert.All(collections, item => Assert.False(item.IsPublished));
        Assert.Contains(collections, item => item.Slug == "gamer-favourites");
        Assert.All(collections.SelectMany(item => item.DiscoveryQueries), query =>
            Assert.DoesNotContain("Nintendo", query, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SaveAsync_BlocksNamedBrandDiscoveryTerms()
    {
        var factory = await CreateDatabaseAsync();
        var service = CreateService(factory);
        var update = ValidUpdate() with
        {
            DisplayName = "Nintendo Favourites",
            DiscoveryQueries = ["Nintendo plush toy"]
        };

        var result = await service.SaveAsync(update);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors!, error => error.Contains("restricted brand", StringComparison.OrdinalIgnoreCase));
        await using var context = factory.CreateDbContext();
        Assert.Empty(context.Collections);
    }

    [Fact]
    public async Task SetPublicationAsync_BlocksThinCollection()
    {
        var factory = await CreateDatabaseAsync();
        var service = CreateService(factory);
        var saved = await service.SaveAsync(ValidUpdate());

        var result = await service.SetPublicationAsync("plushies", saved.CollectionId!.Value, true);

        Assert.False(result.Succeeded);
        Assert.Contains("more indexable products", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetPublicationAsync_PublishesOnlyAfterIndexableTargetIsMet()
    {
        var factory = await CreateDatabaseAsync();
        var service = CreateService(factory);
        var saved = await service.SaveAsync(ValidUpdate());
        await using (var context = factory.CreateDbContext())
        {
            var shopId = await context.Shops.Select(item => item.Id).SingleAsync();
            for (var index = 1; index <= 8; index++)
            {
                var productId = $"product-{index}";
                AddIndexableProduct(context, shopId, saved.CollectionId!.Value, productId, index);
            }
            await context.SaveChangesAsync();
        }

        var result = await service.SetPublicationAsync("plushies", saved.CollectionId!.Value, true);
        var summary = Assert.Single(await service.GetCollectionsAsync("plushies"));

        Assert.True(result.Succeeded);
        Assert.True(summary.IsPublished);
        Assert.Equal(8, summary.IndexableProducts);
        Assert.True(summary.CanPublish);
    }

    [Fact]
    public async Task SetMembershipAsync_AllowsPreclassificationButDoesNotCountPendingProductAsPublic()
    {
        var factory = await CreateDatabaseAsync();
        var service = CreateService(factory);
        var saved = await service.SaveAsync(ValidUpdate());
        await using (var context = factory.CreateDbContext())
        {
            var shopId = await context.Shops.Select(item => item.Id).SingleAsync();
            AddPendingProduct(context, shopId, "pending-product");
            await context.SaveChangesAsync();
        }

        var result = await service.SetMembershipAsync(
            "plushies", saved.CollectionId!.Value, "pending-product", true, false, 0, "owner@example.test");
        var summary = Assert.Single(await service.GetCollectionsAsync("plushies"));

        Assert.True(result.Succeeded);
        Assert.Equal(1, summary.AssignedProducts);
        Assert.Equal(0, summary.PublicProducts);
        Assert.Equal(0, summary.IndexableProducts);
    }

    [Fact]
    public async Task CollectionDiscovery_AssignsNewProductsWithoutPublishingThem()
    {
        var factory = await CreateDatabaseAsync();
        var collectionService = CreateService(factory);
        var saved = await collectionService.SaveAsync(ValidUpdate());
        var source = new QuerySource();
        var ingestion = new CatalogueIngestionService(
            source,
            factory,
            new FixedTimeProvider(Now),
            new ProductQualityAssessmentService(factory, new FixedTimeProvider(Now)));
        var discovery = new CatalogueCollectionDiscoveryService(
            ingestion,
            factory,
            new FixedTimeProvider(Now));

        var result = await discovery.RunAsync(
            "plushies", saved.CollectionId!.Value, 20, "owner@example.test");
        var summary = Assert.Single(await collectionService.GetCollectionsAsync("plushies"));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.QueriesPlanned);
        Assert.Equal(2, result.ProductsAssigned);
        Assert.Equal(["animal plush toy", "woodland animal plush"], source.Queries);
        Assert.Equal(2, summary.AssignedProducts);
        Assert.Equal(0, summary.PublicProducts);
        Assert.False(summary.IsPublished);
    }

    private static CatalogueCollectionService CreateService(InMemoryFactory factory) =>
        new(factory, new CatalogueSeoPolicy(new FixedTimeProvider(Now)), new FixedTimeProvider(Now));

    private static CollectionUpdate ValidUpdate() => new(
        null,
        "plushies",
        "animal-friends",
        "Animal Friends",
        "Cows, rabbits and other familiar soft animal companions.",
        "Browse a carefully reviewed selection of farmyard, woodland and household animal plushies with useful product information and clear marketplace hand-off details.",
        "Animal Plush Toys & Soft Companions",
        "Browse curated animal plush toys including cows, rabbits, pigs, bears and friendly woodland companions.",
        ["animal plush toy", "woodland animal plush"],
        10,
        8,
        true);

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
            CreatedUtc = Now,
            UpdatedUtc = Now
        });
        await context.SaveChangesAsync();
        return factory;
    }

    private static void AddIndexableProduct(
        AffiliateSuperstoreDbContext context,
        Guid shopId,
        Guid collectionId,
        string productId,
        int order)
    {
        context.Products.Add(new ProductRecord
        {
            AliExpressProductId = productId,
            Title = $"Source {productId}",
            MainImageUrl = $"https://example.test/{productId}.jpg",
            IsEligible = true,
            FirstSeenUtc = Now.AddDays(-2),
            LastSeenUtc = Now,
            LastRefreshedUtc = Now
        });
        context.ShopProducts.Add(new ShopProductRecord
        {
            ShopId = shopId,
            ProductId = productId,
            IsActive = true,
            ReviewStatus = ProductReviewStatus.Approved,
            EditorialTitle = $"Curated animal plush {order}",
            EditorialDescription = "A carefully selected soft animal companion with clear details to help shoppers compare the available marketplace options.",
            FirstIncludedUtc = Now,
            LastIncludedUtc = Now
        });
        context.ProductSnapshots.Add(new ProductSnapshotRecord
        {
            ProductId = productId,
            FetchedUtc = Now.AddDays(-1),
            SalePrice = 8.99m,
            Currency = "GBP"
        });
        context.AffiliateLinks.Add(new AffiliateLinkRecord
        {
            Id = Guid.CreateVersion7(),
            ShopId = shopId,
            ProductId = productId,
            SourceUrl = $"https://www.aliexpress.com/item/{productId}.html",
            PromotionUrl = $"https://s.click.aliexpress.com/e/{productId}",
            TrackingId = "theplushyshop",
            Status = AffiliateLinkStatus.Active,
            GeneratedUtc = Now
        });
        context.CollectionProducts.Add(new CollectionProductRecord
        {
            CollectionId = collectionId,
            ProductId = productId,
            DisplayOrder = order,
            AssignedUtc = Now,
            AssignedBy = "owner@example.test"
        });
    }

    private static void AddPendingProduct(AffiliateSuperstoreDbContext context, Guid shopId, string productId)
    {
        context.Products.Add(new ProductRecord
        {
            AliExpressProductId = productId,
            Title = "Pending frog plush",
            IsEligible = true,
            FirstSeenUtc = Now,
            LastSeenUtc = Now,
            LastRefreshedUtc = Now
        });
        context.ShopProducts.Add(new ShopProductRecord
        {
            ShopId = shopId,
            ProductId = productId,
            IsActive = true,
            ReviewStatus = ProductReviewStatus.Pending,
            FirstIncludedUtc = Now,
            LastIncludedUtc = Now
        });
    }

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

    private sealed class QuerySource : IAffiliateCatalogueSource
    {
        public List<string> Queries { get; } = [];

        public Task<AliExpressPage<AliExpressProduct>> SearchAsync(
            string keywords,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(keywords);
            var id = $"collection-{Queries.Count}";
            var product = new AliExpressProduct(
                id,
                null,
                $"Generic {keywords}",
                $"https://ae01.alicdn.com/kf/{id}.jpg",
                $"https://www.aliexpress.com/item/{id}.html",
                null,
                "8.99",
                "10.99",
                "GBP",
                "7%",
                null,
                "10%",
                "98%",
                100,
                "1",
                "Toys",
                "2",
                "Plush",
                "seller",
                "Generic Store",
                null,
                null);
            return Task.FromResult(new AliExpressPage<AliExpressProduct>([product], pageNumber, 1, 1));
        }

        public Task<IReadOnlyList<AliExpressPromotionLink>> GenerateLinksAsync(
            IReadOnlyCollection<string> sourceUrls,
            string trackingId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AliExpressPromotionLink>>(sourceUrls
                .Select(url => new AliExpressPromotionLink(url, $"https://s.click.aliexpress.com/e/{Math.Abs(url.GetHashCode())}", null))
                .ToArray());
    }
}
