using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class CatalogueGovernanceReportServiceTests
{
    [Fact]
    public async Task GetTodayAsync_SummarisesAutomaticActionsWithoutCreatingFalseAnomalies()
    {
        var now = new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        var shopId = Guid.CreateVersion7();
        var collectionId = Guid.CreateVersion7();
        await using (var context = factory.CreateDbContext())
        {
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
            context.AutonomousCataloguePolicies.Add(new AutonomousCataloguePolicyRecord
            {
                ShopId = shopId,
                Mode = AutonomousCatalogueMode.Automatic,
                ReviewEveryHours = 1,
                MaximumCandidatesPerRun = 6,
                MaximumAutoPublishesPerDay = 2,
                MinimumReadinessScore = 1m,
                DuplicateHoldConfidence = .75m,
                DailyAiBudgetUsd = .25m,
                CreatedUtc = now,
                UpdatedUtc = now,
                UpdatedBy = "test"
            });
            context.Products.Add(new ProductRecord
            {
                AliExpressProductId = "governed-product",
                Title = "Highland cow plush toy",
                MainImageUrl = "https://example.test/cow.jpg",
                IsEligible = true,
                AvailabilityState = ProductAvailabilityState.Available,
                FirstSeenUtc = now.AddDays(-1),
                LastSeenUtc = now,
                LastRefreshedUtc = now
            });
            context.ShopProducts.Add(new ShopProductRecord
            {
                ShopId = shopId,
                ProductId = "governed-product",
                IsActive = true,
                ReviewStatus = ProductReviewStatus.Approved,
                EditorialTitle = "Highland Cow Plush",
                EditorialDescription = "A carefully selected Highland cow plush with clear marketplace details for comparison.",
                FirstIncludedUtc = now,
                LastIncludedUtc = now
            });
            context.AffiliateLinks.Add(new AffiliateLinkRecord
            {
                Id = Guid.CreateVersion7(),
                ShopId = shopId,
                ProductId = "governed-product",
                SourceUrl = "https://www.aliexpress.com/item/governed-product.html",
                PromotionUrl = "https://s.click.aliexpress.com/e/governed-product",
                TrackingId = "theplushyshop",
                Status = AffiliateLinkStatus.Active,
                GeneratedUtc = now
            });
            context.Collections.Add(new CollectionRecord
            {
                Id = collectionId,
                ShopId = shopId,
                Slug = "animal-friends",
                DisplayName = "Animal Friends",
                ShortDescription = "Familiar animal plush companions.",
                IntroductoryCopy = "A carefully reviewed selection of familiar animal plush companions with useful marketplace details.",
                SeoTitle = "Animal Plush Toys and Companions",
                SeoDescription = "Browse a curated collection of animal plush toys with clear marketplace hand-off details.",
                DiscoveryQueriesJson = "[\"highland cow plush\"]",
                MinimumProductsForIndexing = 12,
                IsPublished = true,
                CreatedUtc = now,
                UpdatedUtc = now
            });
            context.AutonomousCatalogueDecisions.Add(new AutonomousCatalogueDecisionRecord
            {
                Id = Guid.CreateVersion7(),
                ShopId = shopId,
                ProductId = "governed-product",
                EditorialVersionNumber = 1,
                Mode = AutonomousCatalogueMode.Automatic,
                Decision = AutonomousCatalogueDecision.WouldPublish,
                Action = AutonomousCatalogueAction.Published,
                ReadinessScore = 1m,
                ReasonCodesJson = "[]",
                Summary = "Published after every gate passed.",
                EvaluatedUtc = now
            });
            context.AiInvocations.Add(new AiInvocationRecord
            {
                Id = Guid.CreateVersion7(),
                Purpose = AiInvocationAuditService.ProductCopyPurpose,
                ProductId = "governed-product",
                Provider = "OpenAI",
                Model = "test-model",
                PromptVersion = CatalogueAiSuggestionService.PromptVersion,
                InputHash = "input",
                CacheKey = "cache",
                Status = AiInvocationStatus.Succeeded,
                RequestedUtc = now,
                CompletedUtc = now,
                EstimatedCostUsd = .001m
            });
            context.CollectionPublicationEvents.Add(new CollectionPublicationEventRecord
            {
                Id = Guid.CreateVersion7(),
                CollectionId = collectionId,
                ShopId = shopId,
                Action = CollectionPublicationAction.Published,
                Mode = CollectionPublicationMode.Automatic,
                Actor = "autonomous collection policy",
                Reason = "Twelve products passed all gates.",
                IndexableProducts = 12,
                RequiredProducts = 12,
                OccurredUtc = now
            });
            await context.SaveChangesAsync();
        }
        var clock = new FixedTimeProvider(now);
        var options = new AutonomousCatalogueOptions { MinimumAutomaticCollectionProducts = 12 };
        var service = new CatalogueGovernanceReportService(
            factory,
            new CatalogueCollectionService(factory, new CatalogueSeoPolicy(clock), clock),
            options,
            clock);

        var report = await service.GetTodayAsync("plushies");

        Assert.True(report.IsHealthy);
        Assert.Equal(1, report.AutomaticDecisions);
        Assert.Equal(1, report.ProductsPublished);
        Assert.Equal(1, report.CollectionsPublished);
        Assert.Equal(1, report.AiCalls);
        Assert.Equal(.001m, report.AiSpendUsd);
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
