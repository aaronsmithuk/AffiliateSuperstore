using System.Security.Cryptography;
using System.Text;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class CatalogueAiSuggestionServiceTests
{
    [Fact]
    public async Task SuggestAsync_ReturnsAReviewDraftWithoutChangingTheCatalogue()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "1001", "30cm Highland cow soft plush toy");
        var provider = new FakeProvider(new ProductEditorialSuggestionOutput(
            "30cm Highland Cow Plush",
            "A soft Highland cow plush with a shaggy look and a compact 30cm size for a playful display.",
            ["30cm size"],
            ["merchant keyword repetition"],
            ["Material is not confirmed."],
            "en-GB",
            "fake",
            "test-model",
            Hash("response"),
            120,
            40));
        var service = CreateService(factory, provider);

        var result = await service.SuggestAsync("plushies", "1001");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Suggestion);
        Assert.Equal("30cm Highland Cow Plush", result.Suggestion.SuggestedTitle);
        Assert.Equal(CatalogueAiSuggestionService.PromptVersion, provider.LastRequest!.PromptVersion);
        Assert.Equal(64, provider.LastRequest.InputHash.Length);
        Assert.Contains(provider.LastRequest.Facts, fact => fact.Field == "sourceTitle");
        await using var context = factory.CreateDbContext();
        var product = await context.ShopProducts.SingleAsync();
        Assert.Null(product.EditorialTitle);
        Assert.Null(product.EditorialDescription);
        Assert.Empty(await context.EditorialVersions.ToListAsync());
    }

    [Fact]
    public async Task SuggestAsync_BlocksUnsupportedModelClaims()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "1002", "Highland cow plush toy");
        var provider = new FakeProvider(new ProductEditorialSuggestionOutput(
            "Official 50cm Cotton Highland Cow",
            "An officially licensed, child-safe cotton plush with next-day delivery.",
            [], [], [], "en-GB", "fake", "test-model", Hash("unsafe")));
        var service = CreateService(factory, provider);

        var result = await service.SuggestAsync("plushies", "1002");

        Assert.False(result.Succeeded);
        Assert.True(result.IsBlocked);
        Assert.Contains(result.Findings!, finding => finding.Code == "claim.authenticity");
        Assert.Contains(result.Findings!, finding => finding.Code == "claim.unsupported-number");
        Assert.Contains(result.Findings!, finding => finding.Code == "claim.unsupported-material");
        Assert.Contains(result.Findings!, finding => finding.Code == "claim.safety");
        Assert.Contains(result.Findings!, finding => finding.Code == "claim.delivery");
    }

    [Fact]
    public async Task SuggestAsync_BlocksTheEarlierHighlandCowPilotDraft()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "1005011692664194", "Adorable Highland Cattle Plush Toy 45cm - Huggable Running Cow Stuffed Animal Made with Premium Soft Fabric, Soothing Companion");
        var provider = new FakeProvider(new ProductEditorialSuggestionOutput(
            "45cm Highland Cattle Plush Toy",
            "A 45cm Highland cattle plush toy described as huggable and made with premium soft fabric. The source title also describes it as a running cow stuffed animal and soothing companion.",
            ["45cm", "Highland cattle", "huggable", "premium soft fabric", "running cow", "soothing companion"],
            ["delivery reference", "seller and SKU"],
            ["Supplier claims have not been independently verified."],
            "en-GB",
            "fake",
            "test-model",
            Hash("highland-pilot")));
        var service = CreateService(factory, provider);

        var result = await service.SuggestAsync("plushies", "1005011692664194");

        Assert.False(result.Succeeded);
        Assert.True(result.IsBlocked);
        Assert.Contains(result.Findings!, finding => finding.Code == "copy.promotional-language");
        Assert.Contains(result.Findings!, finding => finding.Code == "copy.source-narration");
        await using var context = factory.CreateDbContext();
        var product = await context.ShopProducts.SingleAsync();
        Assert.Null(product.EditorialTitle);
        Assert.Null(product.EditorialDescription);
        Assert.Empty(await context.EditorialVersions.ToListAsync());
    }

    [Fact]
    public async Task SuggestAsync_DoesNotCallAnUnavailableProvider()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        var service = CreateService(factory, new UnavailableStructuredSuggestionProvider());

        var result = await service.SuggestAsync("plushies", "missing");

        Assert.False(result.Succeeded);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuggestAsync_RecordsIncompleteProviderCopyAsBlockedValidationEvidence()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory, "1003", "Highland cow plush toy");
        var invocationId = Guid.CreateVersion7();
        await using (var context = factory.CreateDbContext())
        {
            context.AiInvocations.Add(new AiInvocationRecord
            {
                Id = invocationId,
                Purpose = AiInvocationAuditService.ProductCopyPurpose,
                ProductId = "1003",
                Provider = "fake",
                Model = "test-model",
                PromptVersion = CatalogueAiSuggestionService.PromptVersion,
                InputHash = Hash("input"),
                CacheKey = Hash("cache"),
                Status = AiInvocationStatus.Succeeded,
                RequestedUtc = DateTimeOffset.UtcNow,
                CompletedUtc = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }
        var provider = new FakeProvider(new ProductEditorialSuggestionOutput(
            "",
            "A useful description without a title.",
            [], [], [], "en-GB", "fake", "test-model", Hash("incomplete"), InvocationId: invocationId));
        var service = CreateService(factory, provider);

        var result = await service.SuggestAsync("plushies", "1003");

        Assert.False(result.Succeeded);
        Assert.True(result.IsBlocked);
        await using var verify = factory.CreateDbContext();
        var invocation = await verify.AiInvocations.SingleAsync();
        Assert.Equal(EditorialValidationState.Blocked, invocation.EditorialValidationState);
        Assert.Contains("ai.incomplete-copy", invocation.ValidationFindingsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunShadowAsync_ClampsTheSampleToTenAndNeverChangesTheCatalogue()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedManyAsync(factory, 12);
        var provider = new FakeProvider(new ProductEditorialSuggestionOutput(
            "Highland Cow Plush",
            "A Highland cow plush with a shaggy character-inspired look and rounded styling for a cheerful collectable display.",
            ["Highland cow plush"],
            ["merchant keyword repetition"],
            [],
            "en-GB",
            "fake",
            "test-model",
            Hash("shadow-response"),
            100,
            30));
        var service = CreateService(factory, provider);

        var result = await service.RunShadowAsync("plushies", 99);

        Assert.Equal(CatalogueAiSuggestionService.MaximumShadowSampleSize, result.RequestedCount);
        Assert.Equal(10, result.SelectedCount);
        Assert.Equal(10, result.CompletedCount);
        Assert.Equal(10, result.SucceededCount);
        Assert.Equal(10, provider.Requests.Count);
        Assert.Equal(1_000, result.InputTokens);
        Assert.Equal(300, result.OutputTokens);
        Assert.Contains("No catalogue copy was saved", result.Message, StringComparison.Ordinal);
        await using var context = factory.CreateDbContext();
        Assert.All(await context.ShopProducts.ToListAsync(), product =>
        {
            Assert.Null(product.EditorialTitle);
            Assert.Null(product.EditorialDescription);
            Assert.Null(product.CurrentEditorialVersionNumber);
        });
        Assert.Empty(await context.EditorialVersions.ToListAsync());
    }

    private static async Task SeedAsync(InMemoryFactory factory, string productId, string sourceTitle)
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
            FirstLevelCategoryName = "Toys & hobbies",
            SecondLevelCategoryName = "Stuffed animals",
            IsEligible = true,
            FirstSeenUtc = now,
            LastSeenUtc = now,
            LastRefreshedUtc = now
        });
        context.ShopProducts.Add(new ShopProductRecord
        {
            ShopId = shopId,
            ProductId = productId,
            FirstIncludedUtc = now,
            LastIncludedUtc = now
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedManyAsync(InMemoryFactory factory, int count)
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
        for (var index = 1; index <= count; index++)
        {
            var productId = $"shadow-{index:D2}";
            context.Products.Add(new ProductRecord
            {
                AliExpressProductId = productId,
                Title = $"Highland cow plush toy variant {index}",
                FirstLevelCategoryName = "Toys & hobbies",
                SecondLevelCategoryName = "Stuffed animals",
                IsEligible = true,
                FirstSeenUtc = now,
                LastSeenUtc = now,
                LastRefreshedUtc = now.AddMinutes(-index)
            });
            context.ShopProducts.Add(new ShopProductRecord
            {
                ShopId = shopId,
                ProductId = productId,
                FirstIncludedUtc = now,
                LastIncludedUtc = now
            });
        }
        await context.SaveChangesAsync();
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static CatalogueAiSuggestionService CreateService(
        InMemoryFactory factory,
        IStructuredSuggestionProvider provider)
    {
        var options = new AiAutomationOptions();
        return new CatalogueAiSuggestionService(
            factory,
            provider,
            new EditorialContentValidator(),
            new AiInvocationAuditService(factory, options, TimeProvider.System),
            options);
    }

    private sealed class FakeProvider(ProductEditorialSuggestionOutput output) : IStructuredSuggestionProvider
    {
        public bool IsAvailable => true;
        public string AvailabilityMessage => "Available";
        public List<ProductEditorialSuggestionRequest> Requests { get; } = [];
        public ProductEditorialSuggestionRequest? LastRequest => Requests.LastOrDefault();

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
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>().UseInMemoryDatabase(databaseName).Options);
    }
}
