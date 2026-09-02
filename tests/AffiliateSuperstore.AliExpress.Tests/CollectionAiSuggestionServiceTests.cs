using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class CollectionAiSuggestionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GenerateAsync_SavesEvidenceBackedSuggestionWithoutCreatingCollection()
    {
        var factory = await CreateDatabaseAsync();
        var service = CreateService(factory, new FakeProvider(ValidOutput()));

        var result = await service.GenerateAsync("plushies", 3);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(1, result.DraftsSaved);
        await using var context = factory.CreateDbContext();
        var suggestion = await context.CollectionSuggestions.SingleAsync();
        Assert.Equal(CollectionSuggestionStatus.Draft, suggestion.Status);
        Assert.Empty(await context.Collections.ToListAsync());
    }

    [Fact]
    public async Task AcceptAsync_CreatesOnlyAnUnpublishedCollectionDraft()
    {
        var factory = await CreateDatabaseAsync();
        var service = CreateService(factory, new FakeProvider(ValidOutput()));
        await service.GenerateAsync("plushies", 3);
        await using var read = factory.CreateDbContext();
        var suggestionId = await read.CollectionSuggestions.Select(item => item.Id).SingleAsync();

        var result = await service.CreateCollectionDraftAsync("plushies", suggestionId, "owner@example.test");

        Assert.True(result.Succeeded, $"{result.Message} {string.Join("; ", result.Errors ?? [])}");
        await using var context = factory.CreateDbContext();
        var collection = await context.Collections.SingleAsync();
        Assert.False(collection.IsPublished);
        Assert.Equal(CollectionSuggestionStatus.Accepted, (await context.CollectionSuggestions.SingleAsync()).Status);
    }

    [Fact]
    public async Task GenerateAsync_DropsSuggestionsWithoutThreeKnownEvidenceProducts()
    {
        var factory = await CreateDatabaseAsync();
        var invalid = ValidOutput() with
        {
            Suggestions = [ValidOutput().Suggestions[0] with { EvidenceProductIds = ["product-1", "unknown"] }]
        };
        var service = CreateService(factory, new FakeProvider(invalid));

        var result = await service.GenerateAsync("plushies", 3);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.DraftsSaved);
        await using var context = factory.CreateDbContext();
        Assert.Empty(await context.CollectionSuggestions.ToListAsync());
    }

    [Fact]
    public async Task GenerateAsync_TrimsLargeCataloguePacketWithoutDroppingBelowMinimum()
    {
        var factory = await CreateDatabaseAsync(120, 900);
        var provider = new FakeProvider(ValidOutput());
        var service = CreateService(factory, provider);

        var result = await service.GenerateAsync("plushies", 3);

        Assert.True(result.Succeeded, result.Message);
        var request = Assert.IsType<CollectionSuggestionRequest>(provider.LastRequest);
        var serialized = JsonSerializer.Serialize(new
        {
            task = "Suggest evidence-backed, generic collection drafts for administrator review.",
            shop = new { slug = request.ShopSlug, name = request.ShopName },
            maximumSuggestions = request.MaximumSuggestions,
            existingCollections = request.ExistingCollections,
            products = request.Products
        });
        Assert.InRange(request.Products.Count, 6, 119);
        Assert.True(serialized.Length <= 16_000);
    }

    private static CollectionAiSuggestionService CreateService(InMemoryFactory factory, ICollectionSuggestionProvider provider)
    {
        var clock = new FixedTimeProvider(Now);
        return new CollectionAiSuggestionService(
            factory,
            provider,
            new CatalogueCollectionService(factory, new CatalogueSeoPolicy(clock), clock),
            new ProductQualityAssessmentService(factory, clock),
            new AiAutomationOptions(),
            clock);
    }

    private static CollectionSuggestionOutput ValidOutput() => new(
        [new SuggestedCollectionDraft(
            "Animal Friends",
            "Friendly animal plush toys gathered into one easy-to-browse collection.",
            "Browse a varied selection of soft animal companions, based on products already present in the reviewed catalogue. Each product still follows the normal suitability and publication review before it can appear publicly.",
            "Animal Plush Toys & Soft Friends",
            "Browse a curated collection of animal plush toys and soft companions from the existing catalogue.",
            ["animal plush toy", "woodland animal plush"],
            "Six eligible animal plush products support a useful broad collection.",
            ["product-1", "product-2", "product-3", "product-4"] )],
        "Test",
        "test-model",
        new string('a', 64),
        100,
        50,
        Guid.CreateVersion7());

    private static async Task<InMemoryFactory> CreateDatabaseAsync(int productCount = 6, int titlePadding = 0)
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        var shopId = Guid.CreateVersion7();
        await using var context = factory.CreateDbContext();
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
        for (var index = 1; index <= productCount; index++)
        {
            var productId = $"product-{index}";
            context.Products.Add(new ProductRecord
            {
                AliExpressProductId = productId,
                Title = $"Animal plush toy {index} {new string('x', titlePadding)}".Trim(),
                FirstLevelCategoryName = "Toys",
                SecondLevelCategoryName = "Plush Animals",
                IsEligible = true,
                AvailabilityState = ProductAvailabilityState.Available,
                FirstSeenUtc = Now.AddDays(-2),
                LastSeenUtc = Now,
                LastRefreshedUtc = Now
            });
            context.ShopProducts.Add(new ShopProductRecord
            {
                ShopId = shopId,
                ProductId = productId,
                IsActive = true,
                ReviewStatus = ProductReviewStatus.Approved,
                EditorialTitle = $"Curated animal plush {index} {new string('x', titlePadding)}".Trim(),
                FirstIncludedUtc = Now,
                LastIncludedUtc = Now
            });
        }
        await context.SaveChangesAsync();
        return factory;
    }

    private sealed class FakeProvider(CollectionSuggestionOutput output) : ICollectionSuggestionProvider
    {
        public bool IsAvailable => true;
        public string AvailabilityMessage => "Available";
        public CollectionSuggestionRequest? LastRequest { get; private set; }

        public Task<CollectionSuggestionOutput> SuggestCollectionsAsync(CollectionSuggestionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(output);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>().UseInMemoryDatabase(databaseName).Options);
    }
}
