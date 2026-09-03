using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AutonomousCatalogueEvaluationServiceTests
{
    [Fact]
    public async Task RunAsync_ShadowModeRecordsWouldPublishWithoutApprovingProduct()
    {
        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, now);
        var aiOptions = new AiAutomationOptions
        {
            Enabled = true,
            ProductCopyEnabled = true,
            ApiKey = "test-key",
            MaximumReservedCostPerCallUsd = .01m
        };
        var autonomousOptions = new AutonomousCatalogueOptions
        {
            Enabled = true,
            AutomaticPublishingEnabled = false
        };
        var clock = new FixedTimeProvider(now);
        var validator = new EditorialContentValidator();
        var quality = new ProductQualityAssessmentService(factory, clock);
        var editorial = new CatalogueEditorialService(factory, quality, validator, clock);
        var suggestions = new CatalogueAiSuggestionService(
            factory,
            new FailingProvider(),
            validator,
            new AiInvocationAuditService(factory, aiOptions, clock),
            aiOptions);
        var queue = new CatalogueAiQueuePreparationService(factory, suggestions, editorial, quality, aiOptions);
        var policy = new AutonomousCataloguePolicyService(factory, autonomousOptions, clock);
        var service = new AutonomousCatalogueEvaluationService(
            factory,
            policy,
            queue,
            new CatalogueAiReviewService(factory),
            editorial,
            new AutonomousCatalogueSafetyService(factory, autonomousOptions, clock),
            autonomousOptions,
            new CatalogueAutomationOptions { ProductStaleAfterHours = 30 },
            aiOptions,
            clock);

        var result = await service.RunAsync("plushies");

        Assert.Equal(1, result.ProductsEvaluated);
        Assert.Equal(1, result.WouldPublish);
        Assert.Equal(0, result.Held);
        Assert.Equal(0, result.Published);
        Assert.Contains("Nothing was published", result.Message, StringComparison.Ordinal);
        await using var context = factory.CreateDbContext();
        Assert.Equal(ProductReviewStatus.Pending, (await context.ShopProducts.SingleAsync()).ReviewStatus);
        var decision = await context.AutonomousCatalogueDecisions.SingleAsync();
        Assert.Equal(AutonomousCatalogueDecision.WouldPublish, decision.Decision);
        Assert.Equal(AutonomousCatalogueAction.ShadowRecorded, decision.Action);
        Assert.Equal(1m, decision.ReadinessScore);
    }

    [Fact]
    public async Task RunAsync_RestrictedAutomaticModePublishesOneFullyQualifiedProductOnce()
    {
        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, now);
        await using (var context = factory.CreateDbContext())
        {
            var policyRow = await context.AutonomousCataloguePolicies.SingleAsync();
            policyRow.Mode = AutonomousCatalogueMode.Automatic;
            policyRow.MaximumCandidatesPerRun = 1;
            policyRow.MaximumAutoPublishesPerDay = 1;
            await context.SaveChangesAsync();
        }

        var aiOptions = new AiAutomationOptions
        {
            Enabled = true,
            ProductCopyEnabled = true,
            ApiKey = "test-key",
            MaximumReservedCostPerCallUsd = .01m
        };
        var autonomousOptions = new AutonomousCatalogueOptions
        {
            Enabled = true,
            AutomaticPublishingEnabled = true
        };
        var clock = new FixedTimeProvider(now);
        var validator = new EditorialContentValidator();
        var quality = new ProductQualityAssessmentService(factory, clock);
        var editorial = new CatalogueEditorialService(factory, quality, validator, clock);
        var suggestions = new CatalogueAiSuggestionService(
            factory,
            new FailingProvider(),
            validator,
            new AiInvocationAuditService(factory, aiOptions, clock),
            aiOptions);
        var service = new AutonomousCatalogueEvaluationService(
            factory,
            new AutonomousCataloguePolicyService(factory, autonomousOptions, clock),
            new CatalogueAiQueuePreparationService(factory, suggestions, editorial, quality, aiOptions),
            new CatalogueAiReviewService(factory),
            editorial,
            new AutonomousCatalogueSafetyService(factory, autonomousOptions, clock),
            autonomousOptions,
            new CatalogueAutomationOptions { ProductStaleAfterHours = 30 },
            aiOptions,
            clock);

        var first = await service.RunAsync("plushies");
        await using (var firstVerification = factory.CreateDbContext())
        {
            var firstDecision = await firstVerification.AutonomousCatalogueDecisions.SingleAsync();
            Assert.True(first.Published == 1,
                $"{first.Message} {firstDecision.Summary} {firstDecision.ReasonCodesJson}");
        }

        var second = await service.RunAsync("plushies");
        Assert.Equal(0, second.Published);
        await using var verification = factory.CreateDbContext();
        Assert.Equal(ProductReviewStatus.Approved, (await verification.ShopProducts.SingleAsync()).ReviewStatus);
        var decision = await verification.AutonomousCatalogueDecisions.SingleAsync();
        Assert.Equal(AutonomousCatalogueAction.Published, decision.Action);
    }

    [Fact]
    public async Task RunAsync_AutomaticModeRetiresPermanentScopeFailureWithReversibleAuditReason()
    {
        var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, now);
        await using (var context = factory.CreateDbContext())
        {
            var policyRow = await context.AutonomousCataloguePolicies.SingleAsync();
            policyRow.Mode = AutonomousCatalogueMode.Automatic;
            var product = await context.Products.SingleAsync();
            product.Title = "DIY sewing craft kit for a stuffed animal plush";
            var shopProduct = await context.ShopProducts.SingleAsync();
            shopProduct.AutomatedReviewFlags = "[{\"Code\":\"scope.non-plush-product\",\"Message\":\"Not a finished plush product.\"}]";
            await context.SaveChangesAsync();
        }

        var service = CreateService(factory, now, automaticPublishing: true);
        var result = await service.RunAsync("plushies");

        Assert.Equal(1, result.ProductsEvaluated);
        Assert.Equal(0, result.Published);
        await using (var verification = factory.CreateDbContext())
        {
            var product = await verification.ShopProducts.SingleAsync();
            Assert.Equal(ProductReviewStatus.Rejected, product.ReviewStatus);
            Assert.StartsWith(CatalogueAutonomousTriagePolicy.AutomaticRetirementReasonPrefix, product.DisabledReason, StringComparison.Ordinal);
            var decision = await verification.AutonomousCatalogueDecisions.SingleAsync();
            Assert.Equal(AutonomousCatalogueAction.None, decision.Action);
            Assert.Contains("retirement.scope.non-plush-product", decision.ReasonCodesJson, StringComparison.Ordinal);
        }

        var editorial = CreateEditorialService(factory, now);
        var reversal = await editorial.SetReviewStatusAsync("plushies", "shadow-product", ProductReviewStatus.NeedsReview);
        Assert.True(reversal.Succeeded);
        await using var reversed = factory.CreateDbContext();
        Assert.Equal(ProductReviewStatus.NeedsReview, (await reversed.ShopProducts.SingleAsync()).ReviewStatus);
        Assert.Null((await reversed.ShopProducts.SingleAsync()).DisabledReason);
    }

    [Fact]
    public async Task RunAsync_DailyLimitHoldSleepsUntilNextUtcDay()
    {
        var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, now);
        await using (var context = factory.CreateDbContext())
        {
            var policyRow = await context.AutonomousCataloguePolicies.SingleAsync();
            policyRow.Mode = AutonomousCatalogueMode.Automatic;
            var shopProduct = await context.ShopProducts.SingleAsync();
            context.AutonomousCatalogueDecisions.Add(new AutonomousCatalogueDecisionRecord
            {
                Id = Guid.CreateVersion7(),
                ShopId = policyRow.ShopId,
                ProductId = shopProduct.ProductId,
                EditorialVersionNumber = shopProduct.CurrentEditorialVersionNumber!.Value,
                Mode = AutonomousCatalogueMode.Automatic,
                Decision = AutonomousCatalogueDecision.Hold,
                Action = AutonomousCatalogueAction.None,
                ReadinessScore = 1m,
                ReasonCodesJson = "[\"publication.daily-limit\"]",
                Summary = "Daily limit reached.",
                EvaluatedUtc = now.AddHours(-2)
            });
            await context.SaveChangesAsync();
        }

        var sameDay = await CreateService(factory, now, automaticPublishing: true).RunAsync("plushies");
        Assert.Equal(0, sameDay.ProductsEvaluated);

        var nextDay = await CreateService(factory, now.AddDays(1), automaticPublishing: true).RunAsync("plushies");
        Assert.Equal(1, nextDay.ProductsEvaluated);
        Assert.Equal(1, nextDay.Published);
    }

    [Fact]
    public async Task GrowthPipeline_ExplainsReadyDraftAndCollectionGap()
    {
        var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, now);
        var clock = new FixedTimeProvider(now);
        var autonomousOptions = new AutonomousCatalogueOptions { Enabled = true };
        var pipelineService = new CatalogueGrowthPipelineService(
            factory,
            new CatalogueAiReviewService(factory),
            new CatalogueCollectionService(factory, new CatalogueSeoPolicy(clock), clock),
            new AutonomousCataloguePolicyService(factory, autonomousOptions, clock),
            clock);

        var pipeline = await pipelineService.GetAsync("plushies");

        Assert.Equal(50, pipeline.TargetPublicProducts);
        Assert.Equal(0, pipeline.PublicProducts);
        Assert.Equal(1, pipeline.AwaitingAiReview);
        Assert.Equal(1, pipeline.ReadyForAutonomousApproval);
        Assert.Equal(0, pipeline.PermanentRetirementCandidates);
        Assert.Equal(10, pipeline.OptimisticDaysToTarget);
        var collection = Assert.Single(pipeline.Collections);
        Assert.Equal(12, collection.ProductsNeeded);
        var candidate = Assert.Single(pipeline.Candidates);
        Assert.Equal("Ready for autonomous approval", candidate.Disposition);
        Assert.Empty(candidate.ReasonCodes);
    }

    private static AutonomousCatalogueEvaluationService CreateService(
        InMemoryFactory factory,
        DateTimeOffset now,
        bool automaticPublishing)
    {
        var aiOptions = new AiAutomationOptions
        {
            Enabled = true,
            ProductCopyEnabled = true,
            ApiKey = "test-key",
            MaximumReservedCostPerCallUsd = .01m
        };
        var autonomousOptions = new AutonomousCatalogueOptions
        {
            Enabled = true,
            AutomaticPublishingEnabled = automaticPublishing
        };
        var clock = new FixedTimeProvider(now);
        var validator = new EditorialContentValidator();
        var quality = new ProductQualityAssessmentService(factory, clock);
        var editorial = new CatalogueEditorialService(factory, quality, validator, clock);
        var suggestions = new CatalogueAiSuggestionService(
            factory,
            new FailingProvider(),
            validator,
            new AiInvocationAuditService(factory, aiOptions, clock),
            aiOptions);
        return new AutonomousCatalogueEvaluationService(
            factory,
            new AutonomousCataloguePolicyService(factory, autonomousOptions, clock),
            new CatalogueAiQueuePreparationService(factory, suggestions, editorial, quality, aiOptions),
            new CatalogueAiReviewService(factory),
            editorial,
            new AutonomousCatalogueSafetyService(factory, autonomousOptions, clock),
            autonomousOptions,
            new CatalogueAutomationOptions { ProductStaleAfterHours = 30 },
            aiOptions,
            clock);
    }

    private static CatalogueEditorialService CreateEditorialService(InMemoryFactory factory, DateTimeOffset now)
    {
        var clock = new FixedTimeProvider(now);
        return new CatalogueEditorialService(
            factory,
            new ProductQualityAssessmentService(factory, clock),
            new EditorialContentValidator(),
            clock);
    }

    private static async Task SeedAsync(InMemoryFactory factory, DateTimeOffset now)
    {
        await using var context = factory.CreateDbContext();
        var shopId = Guid.CreateVersion7();
        var collectionId = Guid.CreateVersion7();
        var versionId = Guid.CreateVersion7();
        var invocationId = Guid.CreateVersion7();
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
            Mode = AutonomousCatalogueMode.Shadow,
            ReviewEveryHours = 24,
            MaximumCandidatesPerRun = 5,
            MaximumAutoPublishesPerDay = 5,
            MinimumReadinessScore = .98m,
            DuplicateHoldConfidence = .85m,
            DailyAiBudgetUsd = .10m,
            CreatedUtc = now,
            UpdatedUtc = now,
            UpdatedBy = "test"
        });
        context.Products.Add(new ProductRecord
        {
            AliExpressProductId = "shadow-product",
            Title = "Highland cow plush toy",
            ProductDetailUrl = "https://www.aliexpress.com/item/shadow-product.html",
            MainImageUrl = "https://example.test/product.jpg",
            FirstLevelCategoryName = "Toys & hobbies",
            SecondLevelCategoryName = "Stuffed animals",
            SellerName = "Example seller",
            IsEligible = true,
            FirstSeenUtc = now.AddDays(-1),
            LastSeenUtc = now,
            LastRefreshedUtc = now,
            LastCheckedUtc = now.AddHours(-1),
            AvailabilityState = ProductAvailabilityState.Available
        });
        context.ShopProducts.Add(new ShopProductRecord
        {
            ShopId = shopId,
            ProductId = "shadow-product",
            EditorialTitle = "Highland Cow Plush",
            EditorialDescription = "A Highland cow plush toy presented as an animal figure for collectors who prefer cattle-themed characters.",
            CurrentEditorialVersionNumber = 1,
            EditorialValidationState = EditorialValidationState.Passed,
            AutomatedReviewFlags = "[]",
            ReviewStatus = ProductReviewStatus.Pending,
            FirstIncludedUtc = now,
            LastIncludedUtc = now
        });
        context.ProductSnapshots.Add(new ProductSnapshotRecord
        {
            ProductId = "shadow-product",
            FetchedUtc = now,
            SalePrice = 9.99m,
            OriginalPrice = 12.99m,
            Currency = "GBP"
        });
        context.AffiliateLinks.Add(new AffiliateLinkRecord
        {
            Id = Guid.CreateVersion7(),
            ShopId = shopId,
            ProductId = "shadow-product",
            SourceUrl = "https://www.aliexpress.com/item/shadow-product.html",
            PromotionUrl = "https://s.click.aliexpress.com/e/shadow-product",
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
            ShortDescription = "Animal plush companions.",
            IntroductoryCopy = "A reviewed animal plush collection.",
            SeoTitle = "Animal plush toys",
            SeoDescription = "Reviewed animal plush toys.",
            DiscoveryQueriesJson = "[\"highland cow plush\",\"animal plush\"]",
            IsPublished = true,
            CreatedUtc = now,
            UpdatedUtc = now
        });
        context.CollectionProducts.Add(new CollectionProductRecord
        {
            CollectionId = collectionId,
            ProductId = "shadow-product",
            AssignedUtc = now,
            AssignedBy = "test"
        });
        context.EditorialVersions.Add(new EditorialVersionRecord
        {
            Id = versionId,
            ShopId = shopId,
            ProductId = "shadow-product",
            VersionNumber = 1,
            EditorialTitle = "Highland Cow Plush",
            EditorialDescription = "A Highland cow plush toy presented as an animal figure for collectors who prefer cattle-themed characters.",
            ChangeKind = EditorialVersionChangeKind.Edit,
            ChangeReason = $"AI-assisted review draft (openai/test, product-editorial-v2, invocation {invocationId}); requires administrator approval.",
            CreatedBy = "autonomous shadow via AI queue",
            CreatedUtc = now,
            ValidationState = EditorialValidationState.Passed,
            ValidationFindingsJson = "[]",
            ValidatorVersion = EditorialContentValidator.Version,
            ContentHash = "shadow-content-hash"
        });
        context.AiInvocations.Add(new AiInvocationRecord
        {
            Id = invocationId,
            Purpose = AiInvocationAuditService.ProductCopyPurpose,
            ProductId = "shadow-product",
            Provider = "OpenAI",
            Model = "test-model",
            PromptVersion = CatalogueAiSuggestionService.PromptVersion,
            InputHash = "input-hash",
            CacheKey = "cache-key",
            Status = AiInvocationStatus.Succeeded,
            RequestedUtc = now.AddMinutes(-1),
            CompletedUtc = now,
            InputTokens = 100,
            OutputTokens = 30,
            EstimatedCostUsd = .001m,
            EditorialValidationState = EditorialValidationState.Passed,
            ValidationFindingsJson = "[]"
        });
        await context.SaveChangesAsync();
    }

    private sealed class FailingProvider : IStructuredSuggestionProvider
    {
        public bool IsAvailable => true;
        public string AvailabilityMessage => "Available";
        public Task<ProductEditorialSuggestionOutput> SuggestProductCopyAsync(
            ProductEditorialSuggestionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The seeded product already has editorial copy and must not invoke the provider.");
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
