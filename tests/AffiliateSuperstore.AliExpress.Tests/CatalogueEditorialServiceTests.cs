using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class CatalogueEditorialServiceTests
{
    [Fact]
    public async Task SaveAsync_NormalisesFieldsAndSetsMerchandisingOptions()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "clean", "Highland cattle plush toy", includeLink: true);
        var service = CreateService(factory);

        var result = await service.SaveAsync(new CatalogueEditorialUpdate(
            "plushies", "clean", "  Highland Cow Plush  ", "  Soft and huggable.  ", true, 12));

        Assert.True(result.Succeeded);
        await using var context = factory.CreateDbContext();
        var item = await context.ShopProducts.SingleAsync();
        Assert.Equal("Highland Cow Plush", item.EditorialTitle);
        Assert.Equal("Soft and huggable.", item.EditorialDescription);
        Assert.True(item.IsFeatured);
        Assert.Equal(12, item.DisplayOrder);
    }

    [Fact]
    public async Task SetReviewStatusAsync_RefusesRiskySourceEvenWhenEditorialTitleLooksSafe()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "risky", "Mimikyu anime plush doll", includeLink: true,
            editorialTitle: "Sweet yellow plush friend");
        var service = CreateService(factory);

        var result = await service.SetReviewStatusAsync("plushies", "risky", ProductReviewStatus.Approved);

        Assert.False(result.Succeeded);
        await using var context = factory.CreateDbContext();
        var item = await context.ShopProducts.SingleAsync();
        Assert.Equal(ProductReviewStatus.NeedsReview, item.ReviewStatus);
        Assert.Contains("ip.third-party-character", item.AutomatedReviewFlags, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_DemotesApprovedProductWhenEditorialCopyIntroducesRisk()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "clean", "Highland cattle plush toy", includeLink: true,
            reviewStatus: ProductReviewStatus.Approved);
        var service = CreateService(factory);

        var result = await service.SaveAsync(new CatalogueEditorialUpdate(
            "plushies", "clean", "Mimikyu plush friend", null, false, 0));

        Assert.True(result.Succeeded);
        await using var context = factory.CreateDbContext();
        var item = await context.ShopProducts.SingleAsync();
        Assert.Equal(ProductReviewStatus.NeedsReview, item.ReviewStatus);
        Assert.Contains("ip.third-party-character", item.AutomatedReviewFlags, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetReviewStatusAsync_RefusesProductWithoutActiveAffiliateLink()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "missing-link", "Highland cattle plush toy", includeLink: false);
        var service = CreateService(factory);

        var result = await service.SetReviewStatusAsync("plushies", "missing-link", ProductReviewStatus.Approved);

        Assert.False(result.Succeeded);
        Assert.Contains("affiliate link", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetReviewStatusAsync_ApprovesCleanEligibleProductWithActiveLink()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "clean", "Highland cattle plush toy", includeLink: true);
        var service = CreateService(factory);

        var result = await service.SetReviewStatusAsync("plushies", "clean", ProductReviewStatus.Approved);

        Assert.True(result.Succeeded);
        await using var context = factory.CreateDbContext();
        Assert.Equal(ProductReviewStatus.Approved, (await context.ShopProducts.SingleAsync()).ReviewStatus);
    }

    private static CatalogueEditorialService CreateService(InMemoryFactory factory) => new(
        factory,
        new ProductQualityAssessmentService(factory, TimeProvider.System),
        TimeProvider.System);

    private static async Task SeedAsync(
        InMemoryFactory factory,
        string productId,
        string sourceTitle,
        bool includeLink,
        string? editorialTitle = null,
        ProductReviewStatus reviewStatus = ProductReviewStatus.Pending)
    {
        await using var context = factory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
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
            CreatedUtc = now,
            UpdatedUtc = now
        });
        context.Products.Add(new ProductRecord
        {
            AliExpressProductId = productId,
            Title = sourceTitle,
            IsEligible = true,
            FirstSeenUtc = now,
            LastSeenUtc = now,
            LastRefreshedUtc = now
        });
        context.ShopProducts.Add(new ShopProductRecord
        {
            ShopId = shopId,
            ProductId = productId,
            IsActive = true,
            ReviewStatus = reviewStatus,
            EditorialTitle = editorialTitle,
            FirstIncludedUtc = now,
            LastIncludedUtc = now
        });
        if (includeLink)
        {
            context.AffiliateLinks.Add(new AffiliateLinkRecord
            {
                Id = Guid.CreateVersion7(),
                ShopId = shopId,
                ProductId = productId,
                SourceUrl = $"https://www.aliexpress.com/item/{productId}.html",
                PromotionUrl = $"https://s.click.aliexpress.com/e/{productId}",
                TrackingId = "theplushyshop",
                Status = AffiliateLinkStatus.Active,
                GeneratedUtc = now
            });
        }
        await context.SaveChangesAsync();
    }

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>().UseInMemoryDatabase(databaseName).Options);
    }
}
