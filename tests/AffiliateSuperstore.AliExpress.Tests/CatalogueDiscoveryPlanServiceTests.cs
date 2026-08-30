using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class CatalogueDiscoveryPlanServiceTests
{
    [Fact]
    public async Task RunAsync_ExecutesConfiguredQueriesAndPagesAsOneGuardedPlan()
    {
        var factory = await CreateDatabaseAsync();
        var source = new FakeSource();
        var ingestion = new CatalogueIngestionService(
            source,
            factory,
            TimeProvider.System,
            new ProductQualityAssessmentService(factory, TimeProvider.System));
        var options = new AffiliateSuperstoreOptions
        {
            Shops =
            [
                new ShopDefinition
                {
                    Slug = "plushies",
                    IsEnabled = true,
                    DefaultSearchQuery = "plush toy",
                    DiscoveryQueries = ["capybara plush", "otter plush"],
                    DiscoveryPagesPerQuery = 2
                }
            ]
        };

        var result = await new CatalogueDiscoveryPlanService(ingestion, options).RunAsync("plushies", 20);

        Assert.Equal(IngestionJobStatus.Succeeded, result.Status);
        Assert.Equal(4, result.RequestsPlanned);
        Assert.Equal(4, result.RequestsCompleted);
        Assert.Equal(4, result.ProductsRead);
        Assert.Equal(4, result.ProductsWritten);
        Assert.Equal(4, result.LinksCreatedOrRefreshed);
        Assert.Equal(
            [("capybara plush", 1), ("capybara plush", 2), ("otter plush", 1), ("otter plush", 2)],
            source.Requests);
        await using var context = factory.CreateDbContext();
        Assert.Equal(4, await context.IngestionJobs.CountAsync());
        Assert.All(
            await context.IngestionJobs.Select(job => job.Checkpoint).ToListAsync(),
            checkpoint => Assert.Contains("keywords=", checkpoint, StringComparison.Ordinal));
    }

    private static async Task<InMemoryFactory> CreateDatabaseAsync()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
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
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        return factory;
    }

    private sealed class FakeSource : IAffiliateCatalogueSource
    {
        public List<(string Keywords, int Page)> Requests { get; } = [];

        public Task<AliExpressPage<AliExpressProduct>> SearchAsync(
            string keywords,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((keywords, pageNumber));
            var id = $"{Requests.Count}";
            var item = new AliExpressProduct(
                id,
                null,
                $"Generic {keywords}",
                $"https://ae01.alicdn.com/kf/{id}.jpg",
                $"https://www.aliexpress.com/item/{id}.html",
                null,
                "8.99",
                "10.99",
                "GBP",
                "7%",
                null,
                "10%",
                "98%",
                100,
                "1",
                "Toys",
                "2",
                "Plush",
                "seller",
                "Generic Store",
                null,
                null);
            return Task.FromResult(new AliExpressPage<AliExpressProduct>([item], pageNumber, 2, 1));
        }

        public Task<IReadOnlyList<AliExpressPromotionLink>> GenerateLinksAsync(
            IReadOnlyCollection<string> sourceUrls,
            string trackingId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AliExpressPromotionLink>>(sourceUrls
                .Select(url => new AliExpressPromotionLink(url, $"https://s.click.aliexpress.com/e/{url.GetHashCode()}", null))
                .ToArray());
    }

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }
}
