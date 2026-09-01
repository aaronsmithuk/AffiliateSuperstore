using System.Text.Json;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class CatalogueAiReviewServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsCurrentAiLineageAndHeldInvocationEvidence()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        var draftInvocationId = Guid.CreateVersion7();
        var editedInvocationId = Guid.CreateVersion7();
        await SeedAsync(factory, draftInvocationId, editedInvocationId);
        var service = new CatalogueAiReviewService(factory);

        var result = await service.GetAsync("plushies");

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.AwaitingReview);
        Assert.Equal(0, result.Approved);
        Assert.Equal(1, result.WarningInvocations);
        Assert.Equal(1, result.BlockedInvocations);
        Assert.Equal(0.003m, result.RecentEstimatedCostUsd);

        var draft = Assert.Single(result.Items, item => item.ProductId == "draft");
        Assert.False(draft.WasHumanEdited);
        Assert.True(draft.CanApprove);
        Assert.Equal(draftInvocationId, draft.Invocation?.Id);
        Assert.Equal("Animal Friends", Assert.Single(draft.Collections));
        Assert.Equal(12.34m, draft.SalePrice);

        var edited = Assert.Single(result.Items, item => item.ProductId == "edited");
        Assert.True(edited.WasHumanEdited);
        Assert.Equal(1, edited.AiDraftVersionNumber);
        Assert.Equal(2, edited.CurrentVersionNumber);
        Assert.Equal("Human-refined Capybara Plush", edited.EditorialTitle);
        Assert.Equal(editedInvocationId, edited.Invocation?.Id);

        Assert.DoesNotContain(result.Items, item => item.ProductId == "manual-only");
        Assert.Contains(result.RecentInvocations, item =>
            item.ProductId == "blocked" &&
            item.ValidationState == EditorialValidationState.Blocked &&
            item.Findings.Single().Code == "claim.unsupported-number");
    }

    [Theory]
    [InlineData("AI-assisted review draft (openai/gpt-test, product-editorial-v2, invocation 018f83f8-9f08-7c78-a8f8-8a5f3b5cc123); requires administrator approval.", "018f83f8-9f08-7c78-a8f8-8a5f3b5cc123")]
    [InlineData("AI-assisted review draft (cache, invocation cache); requires administrator approval.", null)]
    [InlineData("Human review of AI-assisted draft.", null)]
    public void TryReadInvocationId_HandlesAuditedAndNonAuditedReasons(string reason, string? expected)
    {
        var actual = CatalogueAiReviewService.TryReadInvocationId(reason);
        Assert.Equal(expected is null ? null : Guid.Parse(expected), actual);
    }

    private static async Task SeedAsync(InMemoryFactory factory, Guid draftInvocationId, Guid editedInvocationId)
    {
        await using var context = factory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var shopId = Guid.CreateVersion7();
        var collectionId = Guid.CreateVersion7();
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
            CreatedUtc = now,
            UpdatedUtc = now
        });

        AddProduct(context, shopId, "draft", "Capybara plush toy", ProductReviewStatus.NeedsReview, 1, now);
        AddProduct(context, shopId, "edited", "Capybara plush toy", ProductReviewStatus.Pending, 2, now.AddMinutes(-1));
        AddProduct(context, shopId, "manual-only", "Bear plush toy", ProductReviewStatus.NeedsReview, 1, now.AddMinutes(-2));
        context.CollectionProducts.Add(new CollectionProductRecord
        {
            CollectionId = collectionId,
            ProductId = "draft",
            AssignedUtc = now,
            AssignedBy = "test"
        });

        context.EditorialVersions.AddRange(
            AiVersion(shopId, "draft", 1, "Capybara Plush", draftInvocationId, now),
            AiVersion(shopId, "edited", 1, "Capybara Plush", editedInvocationId, now.AddMinutes(-1)),
            Version(shopId, "edited", 2, "Human-refined Capybara Plush", "Human review of AI-assisted draft.", now),
            Version(shopId, "manual-only", 1, "Bear Plush", "Manual curation.", now));

        context.AiInvocations.AddRange(
            Invocation(draftInvocationId, "draft", EditorialValidationState.Passed, now, 0.001m),
            Invocation(editedInvocationId, "edited", EditorialValidationState.Passed, now.AddMinutes(-1), 0.001m),
            Invocation(Guid.CreateVersion7(), "warning", EditorialValidationState.Warning, now.AddMinutes(-2), 0.0005m,
                new EditorialValidationFinding("copy.promotional-language", EditorialFindingSeverity.Warning, "copy", "Remove promotional wording.")),
            Invocation(Guid.CreateVersion7(), "blocked", EditorialValidationState.Blocked, now.AddMinutes(-3), 0.0005m,
                new EditorialValidationFinding("claim.unsupported-number", EditorialFindingSeverity.Blocker, "description", "Unsupported measurement.")));

        context.Products.AddRange(
            ProductOnly("warning", "Promotional plush listing", now),
            ProductOnly("blocked", "Plain plush listing", now));
        await context.SaveChangesAsync();
    }

    private static void AddProduct(
        AffiliateSuperstoreDbContext context,
        Guid shopId,
        string productId,
        string sourceTitle,
        ProductReviewStatus status,
        int currentVersion,
        DateTimeOffset now)
    {
        context.Products.Add(ProductOnly(productId, sourceTitle, now));
        context.ShopProducts.Add(new ShopProductRecord
        {
            ShopId = shopId,
            ProductId = productId,
            EditorialTitle = productId == "edited" ? "Human-refined Capybara Plush" : productId == "manual-only" ? "Bear Plush" : "Capybara Plush",
            EditorialDescription = "A softly styled plush with a rounded animal shape, selected for a characterful collectable display.",
            CurrentEditorialVersionNumber = currentVersion,
            EditorialValidationState = EditorialValidationState.Passed,
            ReviewStatus = status,
            AutomatedReviewFlags = "[]",
            FirstIncludedUtc = now,
            LastIncludedUtc = now
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
            GeneratedUtc = now
        });
        if (productId == "draft")
        {
            context.ProductSnapshots.Add(new ProductSnapshotRecord
            {
                ProductId = productId,
                FetchedUtc = now,
                SalePrice = 12.34m,
                OriginalPrice = 14.99m,
                Currency = "GBP",
                EvaluationRate = 96.5m,
                RecentSalesVolume = 25
            });
        }
    }

    private static ProductRecord ProductOnly(string productId, string title, DateTimeOffset now) => new()
    {
        AliExpressProductId = productId,
        Title = title,
        MainImageUrl = "https://example.test/product.jpg",
        ProductDetailUrl = $"https://www.aliexpress.com/item/{productId}.html",
        FirstLevelCategoryName = "Toys & hobbies",
        SecondLevelCategoryName = "Stuffed animals",
        SellerName = "Example seller",
        IsEligible = true,
        FirstSeenUtc = now,
        LastSeenUtc = now,
        LastRefreshedUtc = now,
        AvailabilityState = ProductAvailabilityState.Available
    };

    private static EditorialVersionRecord AiVersion(
        Guid shopId,
        string productId,
        int version,
        string title,
        Guid invocationId,
        DateTimeOffset createdUtc) => Version(
            shopId,
            productId,
            version,
            title,
            $"AI-assisted review draft (openai/gpt-test, product-editorial-v2, invocation {invocationId}); requires administrator approval.",
            createdUtc);

    private static EditorialVersionRecord Version(
        Guid shopId,
        string productId,
        int version,
        string title,
        string reason,
        DateTimeOffset createdUtc) => new()
        {
            Id = Guid.CreateVersion7(),
            ShopId = shopId,
            ProductId = productId,
            VersionNumber = version,
            EditorialTitle = title,
            EditorialDescription = "A softly styled plush with a rounded animal shape, selected for a characterful collectable display.",
            ChangeKind = EditorialVersionChangeKind.Edit,
            ChangeReason = reason,
            CreatedBy = "reviewer@example.test",
            CreatedUtc = createdUtc,
            ValidationState = EditorialValidationState.Passed,
            ValidationFindingsJson = "[]",
            ValidatorVersion = EditorialContentValidator.Version,
            ContentHash = $"hash-{productId}-{version}"
        };

    private static AiInvocationRecord Invocation(
        Guid id,
        string productId,
        EditorialValidationState validationState,
        DateTimeOffset requestedUtc,
        decimal cost,
        EditorialValidationFinding? finding = null) => new()
        {
            Id = id,
            Purpose = AiInvocationAuditService.ProductCopyPurpose,
            ProductId = productId,
            Provider = "openai",
            Model = "gpt-test",
            PromptVersion = CatalogueAiSuggestionService.PromptVersion,
            InputHash = $"input-{productId}",
            CacheKey = $"cache-{productId}",
            Status = AiInvocationStatus.Succeeded,
            RequestedUtc = requestedUtc,
            CompletedUtc = requestedUtc.AddMilliseconds(250),
            InputTokens = 100,
            OutputTokens = 50,
            EstimatedCostUsd = cost,
            LatencyMilliseconds = 250,
            EditorialValidationState = validationState,
            ValidationFindingsJson = JsonSerializer.Serialize(finding is null ? [] : new[] { finding })
        };

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }
}
