using System.Text.Json;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class CatalogueReadinessServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAsync_CountsOnlyFreshApprovedCompleteQualityClearProductsAsIndexable()
    {
        var factory = await CreateDatabaseAsync();
        var timeProvider = new FixedTimeProvider(Now);

        var report = await new CatalogueReadinessService(
            factory,
            new CatalogueSeoPolicy(timeProvider),
            timeProvider).GetAsync("plushies");

        Assert.Equal(4, report.ActiveProducts);
        Assert.Equal(3, report.QualityClearProducts);
        Assert.Equal(1, report.FlaggedProducts);
        Assert.Equal(1, report.PendingProducts);
        Assert.Equal(1, report.NeedsReviewProducts);
        Assert.Equal(2, report.ApprovedProducts);
        Assert.Equal(4, report.ProductsWithActiveLinks);
        Assert.Equal(2, report.EditoriallyCompleteProducts);
        Assert.Equal(1, report.IndexableProducts);
        Assert.Equal(1, report.StaleProducts);
        Assert.Equal(11, report.ProductsNeededForIndexing);
        Assert.False(report.ShopIsIndexable);
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
            CreatedUtc = Now,
            UpdatedUtc = Now
        });
        AddProduct(context, shopId, "ready", ProductReviewStatus.Approved, Now.AddDays(-1), "[]", true);
        AddProduct(context, shopId, "pending", ProductReviewStatus.Pending, Now.AddDays(-1), "[]", false);
        AddProduct(context, shopId, "flagged", ProductReviewStatus.NeedsReview, Now.AddDays(-1), Flags(), false);
        AddProduct(context, shopId, "stale", ProductReviewStatus.Approved, Now.AddDays(-15), "[]", true);
        await context.SaveChangesAsync();
        return factory;
    }

    private static void AddProduct(
        AffiliateSuperstoreDbContext context,
        Guid shopId,
        string id,
        ProductReviewStatus status,
        DateTimeOffset refreshedUtc,
        string flags,
        bool editoriallyComplete)
    {
        context.Products.Add(new ProductRecord
        {
            AliExpressProductId = id,
            Title = $"Generic {id} plush",
            MainImageUrl = $"https://example.test/{id}.jpg",
            IsEligible = true,
            FirstSeenUtc = refreshedUtc,
            LastSeenUtc = refreshedUtc,
            LastRefreshedUtc = refreshedUtc
        });
        context.ShopProducts.Add(new ShopProductRecord
        {
            ShopId = shopId,
            ProductId = id,
            IsActive = true,
            ReviewStatus = status,
            AutomatedReviewFlags = flags,
            EditorialTitle = editoriallyComplete ? $"Curated {id} plush" : null,
            EditorialDescription = editoriallyComplete ? "A carefully selected soft plush companion with clear product details for informed shopping." : null,
            FirstIncludedUtc = refreshedUtc,
            LastIncludedUtc = refreshedUtc
        });
        context.ProductSnapshots.Add(new ProductSnapshotRecord
        {
            ProductId = id,
            FetchedUtc = refreshedUtc,
            SalePrice = 8.99m,
            Currency = "GBP"
        });
        context.AffiliateLinks.Add(new AffiliateLinkRecord
        {
            Id = Guid.CreateVersion7(),
            ShopId = shopId,
            ProductId = id,
            SourceUrl = $"https://www.aliexpress.com/item/{id}.html",
            PromotionUrl = $"https://s.click.aliexpress.com/e/{id}",
            TrackingId = "theplushyshop",
            Status = AffiliateLinkStatus.Active,
            GeneratedUtc = refreshedUtc
        });
    }

    private static string Flags() => JsonSerializer.Serialize(new[]
    {
        new ProductQualityFlag("ip.third-party-character", "Licensing review required.")
    });

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
