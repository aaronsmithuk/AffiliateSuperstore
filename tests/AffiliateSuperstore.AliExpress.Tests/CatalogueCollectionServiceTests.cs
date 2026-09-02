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
        Assert.Equal(0, summary.ApprovedProducts);
        Assert.Equal(0, summary.PublicProducts);
        Assert.Equal(0, summary.IndexableProducts);
        Assert.Equal(1, summary.AwaitingApprovalProducts);
    }

    [Fact]
    public async Task GetProductCandidatesAsync_SearchesProductIdAndExplainsReadiness()
    {
        var factory = await CreateDatabaseAsync();
        var service = CreateService(factory);
        var saved = await service.SaveAsync(ValidUpdate());
        await using (var context = factory.CreateDbContext())
        {
            var shopId = await context.Shops.Select(item => item.Id).SingleAsync();
            AddPendingProduct(context, shopId, "distinct-product-417");
            await context.SaveChangesAsync();
        }

        var products = await service.GetProductCandidatesAsync(
            "plushies", saved.CollectionId!.Value, "417");

        var product = Assert.Single(products);
        Assert.Equal("distinct-product-417", product.ProductId);
        Assert.False(product.IsPublic);
        Assert.False(product.IsIndexable);
        Assert.Contains("Awaiting editorial approval", product.ReadinessIssues);
        Assert.Contains("No active affiliate link", product.ReadinessIssues);
    }

    [Fact]
    public async Task GetProductCandidatesAsync_SuggestedFilterRanksRelevantProductsWithoutAssigningThem()
    {
        var factory = await CreateDatabaseAsync();
        var service = CreateService(factory);
        var saved = await service.SaveAsync(ValidUpdate() with
        {
            Slug = "weird-wonderful",
            DisplayName = "Weird & Wonderful",
            ShortDescription = "Capybaras, frogs and delightfully unusual plush personalities.",
            DiscoveryQueries = ["frog plush toy", "capybara plush toy"]
        });
        await using (var context = factory.CreateDbContext())
        {
            var shopId = await context.Shops.Select(item => item.Id).SingleAsync();
            AddPendingProduct(context, shopId, "frog-product", "Sleepy green frog plush");
            AddPendingProduct(context, shopId, "unusual-product", "Unusual geometric cushion");
            AddPendingProduct(context, shopId, "bread-product", "Soft bread loaf cushion");
            await context.SaveChangesAsync();
        }

        var products = await service.GetProductCandidatesAsync(
            "plushies",
            saved.CollectionId!.Value,
            filter: CollectionCandidateFilter.Suggested);

        Assert.Equal(2, products.Count);
        var product = products[0];
        Assert.Equal("frog-product", product.ProductId);
        Assert.True(product.IsSuggested);
        Assert.True(product.CollectionMatchScore >= CollectionCandidateMatcher.SuggestedScore);
        Assert.True(product.CollectionMatchScore > products[1].CollectionMatchScore);
        Assert.Equal("unusual-product", products[1].ProductId);
        Assert.Contains(product.CollectionMatchReasons, reason =>
            reason.Contains("frog plush toy", StringComparison.OrdinalIgnoreCase));
        await using var verification = factory.CreateDbContext();
        Assert.Empty(verification.CollectionProducts);
    }

    [Fact]
    public async Task GetCollectionsAsync_ReportsEachReadinessStageAndActionableBlockers()
    {
        var factory = await CreateDatabaseAsync();
        var service = CreateService(factory);
        var saved = await service.SaveAsync(ValidUpdate());
        await using (var context = factory.CreateDbContext())
        {
            var shopId = await context.Shops.Select(item => item.Id).SingleAsync();
            AddIndexableProduct(context, shopId, saved.CollectionId!.Value, "ready-product", 10);
            AddIndexableProduct(context, shopId, saved.CollectionId.Value, "thin-copy-product", 20);
            await context.SaveChangesAsync();
            var thinCopy = await context.ShopProducts.SingleAsync(item => item.ProductId == "thin-copy-product");
            thinCopy.EditorialDescription = "Too short";
            AddPendingProduct(context, shopId, "pending-product");
            context.CollectionProducts.Add(new CollectionProductRecord
            {
                CollectionId = saved.CollectionId.Value,
                ProductId = "pending-product",
                AssignedUtc = Now,
                AssignedBy = "owner@example.test"
            });
            AddApprovedProductWithoutLink(context, shopId, saved.CollectionId.Value, "unlinked-product");
            await context.SaveChangesAsync();
        }

        var summary = Assert.Single(await service.GetCollectionsAsync("plushies"));

        Assert.Equal(4, summary.AssignedProducts);
        Assert.Equal(3, summary.ApprovedProducts);
        Assert.Equal(2, summary.PublicProducts);
        Assert.Equal(1, summary.IndexableProducts);
        Assert.Equal(1, summary.AwaitingApprovalProducts);
        Assert.Equal(1, summary.ApprovedButNotPublicProducts);
        Assert.Equal(1, summary.EditorialBlockerProducts);
        Assert.Equal(0, summary.ImageBlockerProducts);
        Assert.Equal(0, summary.PriceBlockerProducts);
        Assert.Equal(0, summary.FreshnessBlockerProducts);
    }

    [Fact]
    public async Task GetProductCandidatesAsync_PrioritisesAssignedProductsBeforeResultLimit()
    {
        var factory = await CreateDatabaseAsync();
        var service = CreateService(factory);
        var saved = await service.SaveAsync(ValidUpdate());
        await using (var context = factory.CreateDbContext())
        {
            var shopId = await context.Shops.Select(item => item.Id).SingleAsync();
            AddPendingProduct(context, shopId, "unassigned-product");
            AddPendingProduct(context, shopId, "assigned-product");
            context.CollectionProducts.Add(new CollectionProductRecord
            {
                CollectionId = saved.CollectionId!.Value,
                ProductId = "assigned-product",
                AssignedUtc = Now,
                AssignedBy = "owner@example.test"
            });
            await context.SaveChangesAsync();
        }

        var products = await service.GetProductCandidatesAsync(
            "plushies",
            saved.CollectionId!.Value,
            maximumResults: 1);

        var product = Assert.Single(products);
        Assert.Equal("assigned-product", product.ProductId);
        Assert.True(product.IsAssigned);
    }

    [Fact]
    public async Task SetMembershipAsync_CanRemoveAnAssignedProductAfterItBecomesIneligible()
    {
        var factory = await CreateDatabaseAsync();
        var service = CreateService(factory);
        var saved = await service.SaveAsync(ValidUpdate());
        await using (var context = factory.CreateDbContext())
        {
            var shopId = await context.Shops.Select(item => item.Id).SingleAsync();
            AddPendingProduct(context, shopId, "withdrawn-product");
            context.CollectionProducts.Add(new CollectionProductRecord
            {
                CollectionId = saved.CollectionId!.Value,
                ProductId = "withdrawn-product",
                AssignedUtc = Now,
                AssignedBy = "owner@example.test"
            });
            await context.SaveChangesAsync();
            var shopProduct = await context.ShopProducts.SingleAsync(item => item.ProductId == "withdrawn-product");
            shopProduct.IsActive = false;
            var product = await context.Products.SingleAsync(item => item.AliExpressProductId == "withdrawn-product");
            product.IsEligible = false;
            await context.SaveChangesAsync();
        }

        var visible = await service.GetProductCandidatesAsync(
            "plushies", saved.CollectionId!.Value, filter: CollectionCandidateFilter.Assigned);
        var result = await service.SetMembershipAsync(
            "plushies", saved.CollectionId.Value, "withdrawn-product", false, false, 0, "owner@example.test");

        Assert.Single(visible);
        Assert.Contains("Inactive in this shop", visible[0].ReadinessIssues);
        Assert.Contains("Held by catalogue eligibility", visible[0].ReadinessIssues);
        Assert.True(result.Succeeded);
        await using var verification = factory.CreateDbContext();
        Assert.Empty(verification.CollectionProducts);
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

    private static void AddPendingProduct(
        AffiliateSuperstoreDbContext context,
        Guid shopId,
        string productId,
        string title = "Pending frog plush")
    {
        context.Products.Add(new ProductRecord
        {
            AliExpressProductId = productId,
            Title = title,
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

    private static void AddApprovedProductWithoutLink(
        AffiliateSuperstoreDbContext context,
        Guid shopId,
        Guid collectionId,
        string productId)
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
            EditorialTitle = "Curated unlinked animal plush",
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
        context.CollectionProducts.Add(new CollectionProductRecord
        {
            CollectionId = collectionId,
            ProductId = productId,
            AssignedUtc = Now,
            AssignedBy = "owner@example.test"
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
