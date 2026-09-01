using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class CatalogueEditorialServiceTests
{
    private const string PublicationReadyDescription = "A softly styled plush character selected for its friendly shape, expressive details and collectable shelf appeal.";

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
    public async Task SaveAsync_RequiresEvidenceForVerifiedFacts()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "facts-without-evidence", "Pineapple bear plush toy 16cm", includeLink: true);
        var service = CreateService(factory);

        var result = await service.SaveAsync(new CatalogueEditorialUpdate(
            "plushies", "facts-without-evidence", "Pineapple Bear Plush", PublicationReadyDescription, false, 0,
            VerifiedSize: "16 cm tall for the selected option"));

        Assert.False(result.Succeeded);
        Assert.Contains("verification evidence", result.Message, StringComparison.OrdinalIgnoreCase);
        await using var context = factory.CreateDbContext();
        Assert.Empty(await context.EditorialVersions.ToListAsync());
        Assert.Null((await context.ShopProducts.SingleAsync()).VerifiedSize);
    }

    [Fact]
    public async Task SaveAsync_VersionsVerifiedFactsAndReturnsApprovedProductToReview()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "verified-facts", "Pineapple bear plush toy 16cm", includeLink: true,
            reviewStatus: ProductReviewStatus.Approved);
        var service = CreateService(factory);

        var result = await service.SaveAsync(new CatalogueEditorialUpdate(
            "plushies", "verified-facts", "Pineapple Bear Plush", PublicationReadyDescription, false, 0,
            EditedBy: "editor@example.test",
            ChangeReason: "Checked the selected listing option",
            VerifiedSize: "16 cm tall for the selected pictured option",
            VerifiedOptions: "The live listing showed two colour choices for the 16 cm option.",
            VerificationEvidence: "Compared the option selector with the labelled measurement image on 1 September 2026."));

        Assert.True(result.Succeeded);
        await using var context = factory.CreateDbContext();
        var projection = await context.ShopProducts.SingleAsync();
        Assert.Equal("16 cm tall for the selected pictured option", projection.VerifiedSize);
        Assert.Equal(ProductReviewStatus.NeedsReview, projection.ReviewStatus);
        var version = await context.EditorialVersions.SingleAsync();
        Assert.Equal(projection.VerifiedSize, version.VerifiedSize);
        Assert.Contains("option selector", version.VerificationEvidence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetReviewStatusAsync_RefusesRiskySourceEvenWhenEditorialTitleLooksSafe()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "risky", "Mimikyu anime plush doll", includeLink: true,
            editorialTitle: "Sweet yellow plush friend", editorialDescription: PublicationReadyDescription);
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
        await SeedAsync(factory, "missing-link", "Highland cattle plush toy", includeLink: false,
            editorialDescription: PublicationReadyDescription);
        var service = CreateService(factory);

        var result = await service.SetReviewStatusAsync("plushies", "missing-link", ProductReviewStatus.Approved);

        Assert.False(result.Succeeded);
        Assert.Contains("affiliate link", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetReviewStatusAsync_ApprovesCleanEligibleProductWithActiveLink()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "clean", "Highland cattle plush toy", includeLink: true,
            editorialDescription: PublicationReadyDescription);
        var service = CreateService(factory);

        var result = await service.SetReviewStatusAsync("plushies", "clean", ProductReviewStatus.Approved);

        Assert.True(result.Succeeded);
        await using var context = factory.CreateDbContext();
        Assert.Equal(ProductReviewStatus.Approved, (await context.ShopProducts.SingleAsync()).ReviewStatus);
    }

    [Fact]
    public async Task SaveAsync_AppendsImmutableVersionsAndReportsChangedFields()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "versioned", "Highland cattle plush toy 40cm", includeLink: true);
        var service = CreateService(factory);

        var first = await service.SaveAsync(new CatalogueEditorialUpdate(
            "plushies", "versioned", "Highland Cow Plush 40cm", PublicationReadyDescription, false, 3,
            EditedBy: "editor@example.test", ChangeReason: "Initial curation"));
        var second = await service.SaveAsync(new CatalogueEditorialUpdate(
            "plushies", "versioned", "Highland Cow Plush 40cm", PublicationReadyDescription + " It makes a cheerful display companion.", true, 1,
            EditedBy: "editor@example.test", ChangeReason: "Feature on landing page"));

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        await using var context = factory.CreateDbContext();
        var versions = await context.EditorialVersions.OrderBy(item => item.VersionNumber).ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(PublicationReadyDescription, versions[0].EditorialDescription);
        Assert.Equal(EditorialValidationState.Passed, versions[1].ValidationState);
        var history = await service.GetHistoryAsync("plushies", "versioned");
        Assert.Equal(["Description", "Featured state", "Display order"], history[0].ChangedFields);
    }

    [Fact]
    public async Task SaveAsync_BlocksUnsupportedClaimsWithoutChangingProjection()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "claims", "Highland cattle plush toy", includeLink: true);
        var service = CreateService(factory);

        var result = await service.SaveAsync(new CatalogueEditorialUpdate(
            "plushies", "claims", "Official 50cm cotton Highland Cow", PublicationReadyDescription, false, 0));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Findings!, finding => finding.Code == "claim.authenticity");
        Assert.Contains(result.Findings!, finding => finding.Code == "claim.unsupported-number");
        Assert.Contains(result.Findings!, finding => finding.Code == "claim.unsupported-material");
        await using var context = factory.CreateDbContext();
        Assert.Empty(await context.EditorialVersions.ToListAsync());
        Assert.Null((await context.ShopProducts.SingleAsync()).EditorialTitle);
    }

    [Fact]
    public async Task RollbackAsync_CreatesANewRevisionInsteadOfMutatingHistory()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "rollback", "Highland cattle plush toy", includeLink: true);
        var service = CreateService(factory);
        var first = await service.SaveAsync(new CatalogueEditorialUpdate(
            "plushies", "rollback", "Gentle Highland Cow", PublicationReadyDescription, false, 4));
        await service.SaveAsync(new CatalogueEditorialUpdate(
            "plushies", "rollback", "Curious Highland Cow", PublicationReadyDescription + " Its curious expression adds character.", true, 1));

        var result = await service.RollbackAsync("plushies", "rollback", first.VersionId!.Value, "reviewer@example.test");

        Assert.True(result.Succeeded);
        await using var context = factory.CreateDbContext();
        var versions = await context.EditorialVersions.OrderBy(item => item.VersionNumber).ToListAsync();
        Assert.Equal(3, versions.Count);
        Assert.Equal("Gentle Highland Cow", versions[0].EditorialTitle);
        Assert.Equal(EditorialVersionChangeKind.Rollback, versions[2].ChangeKind);
        Assert.Equal(versions[0].Id, versions[2].RolledBackFromVersionId);
        var projection = await context.ShopProducts.SingleAsync();
        Assert.Equal("Gentle Highland Cow", projection.EditorialTitle);
        Assert.Equal(3, projection.CurrentEditorialVersionNumber);
        Assert.Equal(ProductReviewStatus.NeedsReview, projection.ReviewStatus);
    }

    [Fact]
    public async Task SaveAsync_DoesNotAppendIdenticalContentTwice()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "same", "Highland cattle plush toy", includeLink: true);
        var service = CreateService(factory);
        var update = new CatalogueEditorialUpdate("plushies", "same", "Gentle Highland Cow", PublicationReadyDescription, false, 0);

        await service.SaveAsync(update);
        var result = await service.SaveAsync(update);

        Assert.True(result.Succeeded);
        Assert.Contains("No editorial changes", result.Message, StringComparison.Ordinal);
        await using var context = factory.CreateDbContext();
        Assert.Single(await context.EditorialVersions.ToListAsync());
    }

    [Fact]
    public async Task SaveAsync_CanExplicitlyClearExistingEditorialCopy()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "clear", "Highland cattle plush toy", includeLink: true,
            editorialTitle: "Old title", editorialDescription: PublicationReadyDescription);
        var service = CreateService(factory);

        var result = await service.SaveAsync(new CatalogueEditorialUpdate(
            "plushies", "clear", null, null, false, 0, EditedBy: "editor@example.test"));

        Assert.True(result.Succeeded);
        await using var context = factory.CreateDbContext();
        var projection = await context.ShopProducts.SingleAsync();
        Assert.Null(projection.EditorialTitle);
        Assert.Null(projection.EditorialDescription);
        var versions = await context.EditorialVersions.OrderBy(item => item.VersionNumber).ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal("Old title", versions[0].EditorialTitle);
        Assert.Null(versions[1].EditorialTitle);
    }

    [Fact]
    public async Task SaveAsync_RejectsAStaleEditorRowVersion()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "stale", "Highland cattle plush toy", includeLink: true);
        var service = CreateService(factory);

        var result = await service.SaveAsync(new CatalogueEditorialUpdate(
            "plushies", "stale", "Highland Cow Plush", PublicationReadyDescription, false, 0,
            ExpectedRowVersion: Convert.ToBase64String([1, 2, 3])));

        Assert.False(result.Succeeded);
        Assert.Contains("changed after you opened", result.Message, StringComparison.OrdinalIgnoreCase);
        await using var context = factory.CreateDbContext();
        Assert.Empty(await context.EditorialVersions.ToListAsync());
    }

    [Fact]
    public async Task SaveAsync_NoOpStillDemotesApprovedLegacyCopyWithWarnings()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "legacy", "Highland cattle plush toy", includeLink: true,
            editorialTitle: "Highland Cow", editorialDescription: "Friendly plush.", reviewStatus: ProductReviewStatus.Approved);
        var service = CreateService(factory);

        var result = await service.SaveAsync(new CatalogueEditorialUpdate(
            "plushies", "legacy", "Highland Cow", "Friendly plush.", false, 0));

        Assert.True(result.Succeeded);
        await using var context = factory.CreateDbContext();
        var projection = await context.ShopProducts.SingleAsync();
        Assert.Equal(ProductReviewStatus.NeedsReview, projection.ReviewStatus);
        Assert.Equal(EditorialValidationState.Warning, projection.EditorialValidationState);
        Assert.Single(await context.EditorialVersions.ToListAsync());
    }

    private static CatalogueEditorialService CreateService(InMemoryFactory factory) => new(
        factory,
        new ProductQualityAssessmentService(factory, TimeProvider.System),
        new EditorialContentValidator(),
        TimeProvider.System);

    private static async Task SeedAsync(
        InMemoryFactory factory,
        string productId,
        string sourceTitle,
        bool includeLink,
        string? editorialTitle = null,
        string? editorialDescription = null,
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
            EditorialDescription = editorialDescription,
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
