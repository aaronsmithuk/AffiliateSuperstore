using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class ProductIdentityServiceTests
{
    [Fact]
    public async Task RebuildAsync_NormalizesFactsAndClassifiesTrustedConflicts()
    {
        var factory = await CreateDatabaseAsync();
        var service = new ProductIdentityService(factory, TimeProvider.System);

        var result = await service.RebuildAsync("plushies");

        Assert.Equal(11, result.ProductsRead);
        Assert.Equal(11, result.ProfilesUpdated);
        await using var context = factory.CreateDbContext();
        var profiles = await context.ProductIdentityProfiles.ToDictionaryAsync(item => item.ProductId);
        Assert.Equal("4006381333931", profiles["exact-a"].NormalizedGtin);
        Assert.Equal("drg-100", profiles["exact-a"].NormalizedModel);
        Assert.Equal(40m, profiles["exact-a"].SizeCentimetres);
        Assert.Equal(1, profiles["bundle-a"].PackCount);
        Assert.Equal(2, profiles["bundle-b"].PackCount);
        Assert.Null(profiles["multi-size"].SizeCentimetres);

        var candidates = await context.ProductMatchCandidates.ToListAsync();
        Assert.Contains(candidates, item => Pair(item, "exact-a", "exact-b") && item.SuggestedRelationship == ProductRelationship.Duplicate && item.Confidence == .995m);
        Assert.Contains(candidates, item => Pair(item, "bundle-a", "bundle-b") && item.SuggestedRelationship == ProductRelationship.Bundle && item.ConflictJson!.Contains("pack count differs", StringComparison.Ordinal));
        Assert.Contains(candidates, item => Pair(item, "variant-a", "variant-b") && item.SuggestedRelationship == ProductRelationship.Variant && item.ConflictJson!.Contains("size differs", StringComparison.Ordinal));
        Assert.Contains(candidates, item => Pair(item, "model-a", "model-b") && item.SuggestedRelationship == ProductRelationship.Variant && item.ConflictJson!.Contains("model differs", StringComparison.Ordinal));
        Assert.DoesNotContain(candidates, item => Pair(item, "unrelated-a", "unrelated-b"));
    }

    [Fact]
    public async Task RebuildAsync_IsHashIdempotentAndReviewCreatesReversibleCanonicalMembership()
    {
        var factory = await CreateDatabaseAsync();
        var service = new ProductIdentityService(factory, TimeProvider.System);
        var first = await service.RebuildAsync("plushies");
        var second = await service.RebuildAsync("plushies");
        await using var lookup = factory.CreateDbContext();
        var candidateId = await lookup.ProductMatchCandidates
            .Where(item => item.LeftProductId == "exact-a" && item.RightProductId == "exact-b")
            .Select(item => item.Id)
            .SingleAsync();

        var reviewed = await service.ReviewAsync(candidateId, accept: true, "owner@example.test");

        Assert.True(first.ProfilesUpdated > 0);
        Assert.Equal(0, second.ProfilesUpdated);
        Assert.Equal(0, second.CandidatesCreated);
        Assert.True(reviewed.Succeeded);
        await using var context = factory.CreateDbContext();
        Assert.Equal(11, await context.Products.CountAsync());
        Assert.Equal(1, await context.CanonicalProducts.CountAsync());
        Assert.Equal(2, await context.CanonicalProductMembers.CountAsync());
        var reviewedCandidate = await context.ProductMatchCandidates.SingleAsync(item => item.Id == candidateId);
        Assert.Equal(ProductMatchReviewStatus.Accepted, reviewedCandidate.ReviewStatus);
        Assert.Equal("owner@example.test", reviewedCandidate.ReviewedBy);
    }

    [Fact]
    public async Task RebuildAsync_SupersedesPendingCandidateWhenIdentityEvidenceDisappears()
    {
        var factory = await CreateDatabaseAsync();
        var service = new ProductIdentityService(factory, TimeProvider.System);
        await service.RebuildAsync("plushies");
        await using (var update = factory.CreateDbContext())
        {
            var product = await update.Products.SingleAsync(item => item.AliExpressProductId == "bundle-b");
            product.Title = "Purple Octopus Cushion 2 pcs";
            await update.SaveChangesAsync();
        }

        await service.RebuildAsync("plushies");

        await using var context = factory.CreateDbContext();
        var candidate = await context.ProductMatchCandidates.SingleAsync(item => item.LeftProductId == "bundle-a" && item.RightProductId == "bundle-b");
        Assert.Equal(ProductMatchReviewStatus.Pending, candidate.ReviewStatus);
        Assert.False(candidate.IsCurrent);
    }

    private static bool Pair(ProductMatchCandidateRecord item, string left, string right) =>
        item.LeftProductId == left && item.RightProductId == right ||
        item.LeftProductId == right && item.RightProductId == left;

    private static async Task<InMemoryFactory> CreateDatabaseAsync()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
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
        var products = new[]
        {
            Product("exact-a", "Green Dragon Plush DRG-100 40 cm", now, "4006381333931"),
            Product("exact-b", "40 cm Green Dragon Plush DRG-100", now, "4006381333931"),
            Product("bundle-a", "Blue Bear Plush 30 cm 1 pcs", now),
            Product("bundle-b", "Blue Bear Plush 30 cm 2 pcs", now),
            Product("variant-a", "Red Fox Plush 20 cm", now),
            Product("variant-b", "Red Fox Plush 40 cm", now),
            Product("unrelated-a", "Yellow Duck Plush 20 cm", now),
            Product("unrelated-b", "Purple Octopus Cushion 40 cm", now),
            Product("multi-size", "Highland Cow Plush 45/65cm", now),
            Product("model-a", "Robot Bear Plush AB-100", now),
            Product("model-b", "Robot Bear Plush AB-200", now)
        };
        context.Products.AddRange(products);
        context.ShopProducts.AddRange(products.Select(product => new ShopProductRecord
        {
            ShopId = shopId,
            ProductId = product.AliExpressProductId,
            IsActive = true,
            FirstIncludedUtc = now,
            LastIncludedUtc = now
        }));
        await context.SaveChangesAsync();
        return factory;
    }

    private static ProductRecord Product(string id, string title, DateTimeOffset now, string? ean = null) => new()
    {
        AliExpressProductId = id,
        Title = title,
        EanCode = ean,
        SecondLevelCategoryId = "plush",
        SecondLevelCategoryName = "Plush",
        IsEligible = true,
        FirstSeenUtc = now,
        LastSeenUtc = now,
        LastRefreshedUtc = now,
        LastCheckedUtc = now
    };

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }
}
