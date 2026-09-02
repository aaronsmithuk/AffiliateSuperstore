using System.Security.Cryptography;
using System.Text;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class CatalogueAiQueuePreparationServiceTests
{
    [Fact]
    public async Task RunAsync_SavesAtMostTenCollectionAssignedDraftsAndNeverApprovesThem()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedQueueAsync(factory, 12, flaggedProductIndex: 11, unassignedProductIndex: 12);
        var provider = new FakeProvider(new ProductEditorialSuggestionOutput(
            "Highland Cow Plush",
            "A Highland cow plush with a shaggy character-inspired look and rounded styling for a cheerful collectable display.",
            ["Highland cow plush"],
            ["merchant keyword repetition"],
            [],
            "en-GB",
            "fake",
            "test-model",
            Hash("queue-response"),
            100,
            30));
        var service = CreateService(factory, provider);

        var result = await service.RunAsync("plushies", 99, "queue tester");

        Assert.Equal(CatalogueAiQueuePreparationService.MaximumBatchSize, result.RequestedCount);
        Assert.Equal(10, result.SelectedCount);
        Assert.Equal(10, result.CompletedCount);
        Assert.Equal(10, result.DraftsSaved);
        Assert.Equal(10, provider.Requests.Count);
        Assert.Equal(1_000, result.InputTokens);
        Assert.Equal(300, result.OutputTokens);
        Assert.Contains("Nothing was approved", result.Message, StringComparison.Ordinal);

        await using var context = factory.CreateDbContext();
        Assert.Equal(10, await context.EditorialVersions.CountAsync());
        Assert.Equal(10, await context.ShopProducts.CountAsync(item => item.EditorialTitle != null));
        Assert.Equal(0, await context.ShopProducts.CountAsync(item => item.ReviewStatus == ProductReviewStatus.Approved));
        Assert.All(await context.EditorialVersions.ToListAsync(), version =>
        {
            Assert.Equal("queue tester via AI queue", version.CreatedBy);
            Assert.Contains("requires administrator approval", version.ChangeReason, StringComparison.Ordinal);
        });
        Assert.Null((await context.ShopProducts.SingleAsync(item => item.ProductId == "queue-11")).EditorialTitle);
        Assert.Null((await context.ShopProducts.SingleAsync(item => item.ProductId == "queue-12")).EditorialTitle);
    }

    [Fact]
    public async Task RunAsync_DoesNotSaveBlockedSuggestions()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedQueueAsync(factory, 1);
        var provider = new FakeProvider(new ProductEditorialSuggestionOutput(
            "Official Cotton Highland Cow Plush",
            "An officially licensed, child-safe cotton plush with next-day delivery and guaranteed quality.",
            [], [], [], "en-GB", "fake", "test-model", Hash("blocked"), 80, 20));
        var service = CreateService(factory, provider);

        var result = await service.RunAsync("plushies", 1, "queue tester");

        Assert.Equal(1, result.SelectedCount);
        Assert.Equal(0, result.DraftsSaved);
        Assert.Equal(1, result.BlockedCount);
        await using var context = factory.CreateDbContext();
        Assert.Empty(await context.EditorialVersions.ToListAsync());
        var product = await context.ShopProducts.SingleAsync();
        Assert.Null(product.EditorialTitle);
        Assert.Equal(ProductReviewStatus.Pending, product.ReviewStatus);
    }

    [Fact]
    public async Task RunAsync_SkipsUnchangedRejectedInputAndAdvancesToNextCandidate()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedQueueAsync(factory, 2);
        var provider = new FakeProvider(new ProductEditorialSuggestionOutput(
            "Official Cotton Highland Cow Plush",
            "An officially licensed, child-safe cotton plush with next-day delivery and guaranteed quality.",
            [], [], [], "en-GB", "fake", "test-model", Hash("blocked-advance"), 80, 20));
        var service = CreateService(factory, provider);

        var first = await service.RunAsync("plushies", 1, "queue tester");
        await using (var context = factory.CreateDbContext())
        {
            context.AiInvocations.Add(new AiInvocationRecord
            {
                Id = Guid.CreateVersion7(),
                Purpose = AiInvocationAuditService.ProductCopyPurpose,
                ProductId = provider.Requests[0].ProductId,
                Provider = "fake",
                Model = "test-model",
                PromptVersion = CatalogueAiSuggestionService.PromptVersion,
                InputHash = provider.Requests[0].InputHash,
                CacheKey = "rejected-input",
                Status = AiInvocationStatus.Succeeded,
                RequestedUtc = DateTimeOffset.UtcNow,
                CompletedUtc = DateTimeOffset.UtcNow,
                EditorialValidationState = EditorialValidationState.Blocked
            });
            await context.SaveChangesAsync();
        }
        var second = await service.RunAsync("plushies", 1, "queue tester");

        Assert.Equal(1, first.BlockedCount);
        Assert.Equal(1, second.BlockedCount);
        Assert.Equal(["queue-01", "queue-02"], provider.Requests.Select(request => request.ProductId));
    }

    [Fact]
    public async Task RunAsync_TruncatesLongAuditIdentityWithoutLosingTheDraft()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedQueueAsync(factory, 1);
        var provider = new FakeProvider(new ProductEditorialSuggestionOutput(
            "Highland Cow Plush",
            "A Highland cow plush with a shaggy character-inspired look and rounded styling for a cheerful collectable display.",
            [], [], [], "en-GB", "fake", new string('m', 600), Hash("long-audit"), 80, 20));
        var service = CreateService(factory, provider);

        var result = await service.RunAsync("plushies", 1, new string('a', 600));

        Assert.Equal(1, result.DraftsSaved);
        Assert.Equal(0, result.FailedCount);
        await using var context = factory.CreateDbContext();
        var version = await context.EditorialVersions.SingleAsync();
        Assert.Equal(CatalogueEditorialService.MaximumActorLength, version.CreatedBy.Length);
        Assert.NotNull(version.ChangeReason);
        Assert.Equal(CatalogueEditorialService.MaximumChangeReasonLength, version.ChangeReason!.Length);
    }

    private static CatalogueAiQueuePreparationService CreateService(
        InMemoryFactory factory,
        IStructuredSuggestionProvider provider)
    {
        var options = new AiAutomationOptions
        {
            Enabled = true,
            ProductCopyEnabled = true,
            ApiKey = "test-key"
        };
        var validator = new EditorialContentValidator();
        var quality = new ProductQualityAssessmentService(factory, TimeProvider.System);
        var editorial = new CatalogueEditorialService(factory, quality, validator, TimeProvider.System);
        var suggestions = new CatalogueAiSuggestionService(
            factory,
            provider,
            validator,
            new AiInvocationAuditService(factory, options, TimeProvider.System),
            options);
        return new CatalogueAiQueuePreparationService(factory, suggestions, editorial, quality, options);
    }

    private static async Task SeedQueueAsync(
        InMemoryFactory factory,
        int count,
        int? flaggedProductIndex = null,
        int? unassignedProductIndex = null)
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
            ShortDescription = "A carefully selected group of familiar animal plush companions.",
            IntroductoryCopy = "A collection of animal plush toys selected from active marketplace listings for editorial review before publication.",
            SeoTitle = "Animal Plush Toys and Soft Companions",
            SeoDescription = "Browse an editorially reviewed collection of animal plush toys and soft companions from active marketplace sellers.",
            DiscoveryQueriesJson = "[]",
            IsPublished = true,
            CreatedUtc = now,
            UpdatedUtc = now
        });

        for (var index = 1; index <= count; index++)
        {
            var productId = $"queue-{index:D2}";
            var sourceTitle = index == flaggedProductIndex
                ? "Hello Kitty character plush toy"
                : "Highland cow plush toy";
            context.Products.Add(new ProductRecord
            {
                AliExpressProductId = productId,
                Title = sourceTitle,
                MainImageUrl = "https://example.test/product.jpg",
                FirstLevelCategoryName = "Toys & hobbies",
                SecondLevelCategoryName = "Stuffed animals",
                IsEligible = true,
                FirstSeenUtc = now,
                LastSeenUtc = now,
                LastRefreshedUtc = now.AddMinutes(-index),
                LastCheckedUtc = now,
                AvailabilityState = ProductAvailabilityState.Available
            });
            context.ShopProducts.Add(new ShopProductRecord
            {
                ShopId = shopId,
                ProductId = productId,
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
            if (index != unassignedProductIndex)
            {
                context.CollectionProducts.Add(new CollectionProductRecord
                {
                    CollectionId = collectionId,
                    ProductId = productId,
                    AssignedUtc = now,
                    AssignedBy = "test"
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class FakeProvider(ProductEditorialSuggestionOutput output) : IStructuredSuggestionProvider
    {
        public bool IsAvailable => true;
        public string AvailabilityMessage => "Available";
        public List<ProductEditorialSuggestionRequest> Requests { get; } = [];

        public Task<ProductEditorialSuggestionOutput> SuggestProductCopyAsync(
            ProductEditorialSuggestionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(output);
        }
    }

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }
}
