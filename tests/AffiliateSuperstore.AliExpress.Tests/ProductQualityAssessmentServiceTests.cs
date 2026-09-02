using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class ProductQualityAssessmentServiceTests
{
    [Fact]
    public void Assess_GenericPlush_HasNoAutomatedFlags()
    {
        var service = new ProductQualityAssessmentService(null!, TimeProvider.System);

        var result = service.Assess("Adorable Highland cattle plush toy", "Toys", "Plush");

        Assert.False(result.RequiresReview);
        Assert.Empty(result.Flags);
    }

    [Theory]
    [InlineData("Mimikyu Eevee anime plush doll", "ip.third-party-character")]
    [InlineData("Cute Stitch and Cinnamoroll plush doll", "ip.third-party-character")]
    [InlineData("Interactive catnip plush toy for cats", "scope.pet-product")]
    [InlineData("Newborn stroller plush baby rattle", "scope.baby-product")]
    [InlineData("Funny cigar design plush toy", "safety.tobacco-themed")]
    [InlineData("2Pcs plush keychain set", "listing.ambiguous-quantity")]
    [InlineData("Kermit The Frog plush hand puppet", "ip.third-party-character")]
    [InlineData("Blue axolotl soft game peluche", "ip.third-party-character")]
    [InlineData("Genuine YELL original otter capsule figure", "ip.licensing-claim")]
    [InlineData("Cute frog plush toy 25/35cm", "listing.variant-dependent-price")]
    [InlineData("Otter plush backpack crossbody bag", "scope.non-plush-product")]
    [InlineData("Kids Sewing Kit Woodland Animals Craft DIY Sewing Felt Plush Animals", "scope.non-plush-product")]
    [InlineData("12-36Sets Adopt A Chick Mini Wind Up Chicks Plush Party Favour", "listing.ambiguous-quantity")]
    public void Assess_RiskyOrOutOfScopeTitle_ReturnsExpectedFlag(string title, string expectedCode)
    {
        var service = new ProductQualityAssessmentService(null!, TimeProvider.System);

        var result = service.Assess(title, "Toys", "Plush");

        Assert.Contains(result.Flags, flag => flag.Code == expectedCode);
    }

    [Theory]
    [InlineData("Universal UK plug wall charger adapter", "Electronics", "Chargers", "scope.missing-plush-evidence")]
    [InlineData("For Tesla Model Y headrest car pillow", "Automotive", "Interior accessories", "scope.missing-plush-evidence")]
    [InlineData("Jeffy hand puppet soft toy", "Toys", "Puppets", "ip.third-party-character")]
    [InlineData("Plush dog chew rope toy", "Pet supplies", "Dog toys", "scope.pet-product")]
    public void Assess_DiscoveryEvidence_ReturnsExpectedFlag(
        string title,
        string firstCategory,
        string secondCategory,
        string expectedCode)
    {
        var service = new ProductQualityAssessmentService(null!, TimeProvider.System);

        var result = service.Assess(title, firstCategory, secondCategory, requirePlushEvidence: true);

        Assert.Contains(result.Flags, flag => flag.Code == expectedCode);
    }

    [Fact]
    public async Task ReassessShopAsync_DemotesFlaggedApprovedProductButNeverAutoApprovesCleanProduct()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory);
        var service = new ProductQualityAssessmentService(factory, TimeProvider.System);

        var result = await service.ReassessShopAsync("plushies");

        Assert.Equal(2, result.ProductsChecked);
        Assert.Equal(1, result.ProductsFlagged);
        Assert.Equal(1, result.ProductsDemoted);
        await using var context = factory.CreateDbContext();
        var risky = await context.ShopProducts.SingleAsync(item => item.ProductId == "risky");
        var clean = await context.ShopProducts.SingleAsync(item => item.ProductId == "clean");
        Assert.Equal(ProductReviewStatus.NeedsReview, risky.ReviewStatus);
        Assert.Contains("ip.third-party-character", risky.AutomatedReviewFlags, StringComparison.Ordinal);
        Assert.Equal(ProductReviewStatus.Pending, clean.ReviewStatus);
    }

    private static async Task SeedAsync(InMemoryFactory factory)
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
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        });
        context.Products.AddRange(
            Product("risky", "Mimikyu Eevee anime plush doll"),
            Product("clean", "Adorable Highland cattle plush toy"));
        context.ShopProducts.AddRange(
            ShopProduct(shopId, "risky", ProductReviewStatus.Approved, "Sweet yellow plush friend"),
            ShopProduct(shopId, "clean", ProductReviewStatus.Pending));
        await context.SaveChangesAsync();
    }

    private static ProductRecord Product(string id, string title) => new()
    {
        AliExpressProductId = id,
        Title = title,
        IsEligible = true,
        FirstSeenUtc = DateTimeOffset.UtcNow,
        LastSeenUtc = DateTimeOffset.UtcNow,
        LastRefreshedUtc = DateTimeOffset.UtcNow
    };

    private static ShopProductRecord ShopProduct(
        Guid shopId,
        string productId,
        ProductReviewStatus status,
        string? editorialTitle = null) => new()
        {
            ShopId = shopId,
            ProductId = productId,
            IsActive = true,
            ReviewStatus = status,
            EditorialTitle = editorialTitle,
            FirstIncludedUtc = DateTimeOffset.UtcNow,
            LastIncludedUtc = DateTimeOffset.UtcNow
        };

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>().UseInMemoryDatabase(databaseName).Options);
    }
}
