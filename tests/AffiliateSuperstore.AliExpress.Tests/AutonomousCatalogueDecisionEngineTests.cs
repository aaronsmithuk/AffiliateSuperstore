using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AutonomousCatalogueDecisionEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Assess_AllHardGatesPass_RecordsWouldPublish()
    {
        var assessment = AutonomousCatalogueDecisionEngine.Assess(
            ValidItem(),
            new AutonomousCatalogueCandidateEvidence(Now.AddHours(-1), null, false, Guid.CreateVersion7()),
            Policy(),
            Now,
            30);

        Assert.Equal(AutonomousCatalogueDecision.WouldPublish, assessment.Decision);
        Assert.Equal(1m, assessment.ReadinessScore);
        Assert.Empty(assessment.ReasonCodes);
    }

    [Fact]
    public void Assess_ProbableDuplicate_IsHeldEvenWhenEveryOtherGatePasses()
    {
        var assessment = AutonomousCatalogueDecisionEngine.Assess(
            ValidItem(),
            new AutonomousCatalogueCandidateEvidence(Now.AddHours(-1), .92m, false, Guid.CreateVersion7()),
            Policy(),
            Now,
            30);

        Assert.Equal(AutonomousCatalogueDecision.Hold, assessment.Decision);
        Assert.Contains("duplicate.probable", assessment.ReasonCodes);
    }

    [Fact]
    public void Assess_StaleUnavailableHumanEditedProduct_ExplainsEveryHold()
    {
        var item = ValidItem() with
        {
            AvailabilityState = ProductAvailabilityState.SuspectedUnavailable,
            CurrentVersionNumber = 2,
            AiDraftVersionNumber = 1
        };
        var assessment = AutonomousCatalogueDecisionEngine.Assess(
            item,
            new AutonomousCatalogueCandidateEvidence(Now.AddHours(-40), null, false, Guid.CreateVersion7()),
            Policy(),
            Now,
            30);

        Assert.Equal(AutonomousCatalogueDecision.Hold, assessment.Decision);
        Assert.Contains("product.unavailable", assessment.ReasonCodes);
        Assert.Contains("source.stale", assessment.ReasonCodes);
        Assert.Contains("editorial.human-edited", assessment.ReasonCodes);
    }

    [Fact]
    public async Task PolicyService_CreatesShadowDefaultAndKeepsAutomaticModeLocked()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedShopAsync(factory);
        var service = new AutonomousCataloguePolicyService(
            factory,
            new AutonomousCatalogueOptions { AutomaticPublishingEnabled = false },
            new FixedTimeProvider(Now));

        Assert.Equal(1, await service.EnsureDefaultsAsync());
        Assert.Equal(0, await service.EnsureDefaultsAsync());
        var policy = Assert.Single(await service.GetAllAsync());
        Assert.Equal(AutonomousCatalogueMode.Shadow, policy.Mode);
        Assert.Equal(5, policy.MaximumCandidatesPerRun);

        var update = await service.UpdateAsync(new AutonomousCataloguePolicyUpdate(
            policy.ShopSlug,
            AutonomousCatalogueMode.Automatic,
            policy.ReviewEveryHours,
            policy.MaximumCandidatesPerRun,
            policy.MaximumAutoPublishesPerDay,
            policy.MinimumReadinessScore,
            policy.DuplicateHoldConfidence,
            policy.DailyAiBudgetUsd,
            "test administrator",
            policy.ExpectedRowVersion));

        Assert.False(update.Succeeded);
        Assert.Contains("global safety switch", update.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DecisionAudit_ProductRelationship_DoesNotCreateSqlServerCascadePath()
    {
        using var context = new InMemoryFactory(Guid.NewGuid().ToString("N")).CreateDbContext();
        var decisionEntity = context.Model.FindEntityType(typeof(AutonomousCatalogueDecisionRecord));
        Assert.NotNull(decisionEntity);
        var protectedAuditRelationships = decisionEntity!.GetForeignKeys()
            .Where(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(ProductRecord)
                                 || foreignKey.PrincipalEntityType.ClrType == typeof(ShopRecord))
            .ToArray();

        Assert.Equal(2, protectedAuditRelationships.Length);
        Assert.All(protectedAuditRelationships, foreignKey =>
            Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior));
    }

    private static AutonomousCataloguePolicy Policy() => new(
        Guid.CreateVersion7(),
        "plushies",
        "The Plushy Shop",
        AutonomousCatalogueMode.Shadow,
        24,
        5,
        5,
        .98m,
        .85m,
        .10m,
        Now,
        "test",
        string.Empty);

    private static CatalogueAiReviewItem ValidItem() => new(
        "product-1",
        "Highland cow plush toy",
        "https://www.aliexpress.com/item/product-1.html",
        "https://example.test/product.jpg",
        "Toys & hobbies",
        "Stuffed animals",
        "Example seller",
        9.99m,
        12.99m,
        "GBP",
        95m,
        100,
        ["Animal Friends"],
        "Highland Cow Plush",
        "A Highland cow plush with a softly rounded design.",
        false,
        0,
        ProductReviewStatus.Pending,
        EditorialValidationState.Passed,
        [],
        [],
        true,
        true,
        ProductAvailabilityState.Available,
        true,
        1,
        1,
        "AI-assisted review draft (openai/test, product-editorial-v2, invocation 00000000-0000-0000-0000-000000000001); requires administrator approval.",
        "autonomous shadow via AI queue",
        Now,
        string.Empty,
        new CatalogueAiReviewInvocation(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "product-1",
            "Highland cow plush toy",
            "OpenAI",
            "test-model",
            CatalogueAiSuggestionService.PromptVersion,
            AiInvocationStatus.Succeeded,
            EditorialValidationState.Passed,
            [],
            Now.AddMinutes(-1),
            Now,
            100,
            30,
            .001m,
            500,
            false,
            null,
            null));

    private static async Task SeedShopAsync(InMemoryFactory factory)
    {
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
}
