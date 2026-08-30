using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AffiliateLinkRenewalServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RunAsync_ReplacesChangedStaleLinkAndLeavesRecentLinkAlone()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory);
        var source = new FakeSource(url => $"https://s.click.aliexpress.com/e/renewed-{url.GetHashCode()}");
        var service = new AffiliateLinkRenewalService(source, factory, new FixedTimeProvider(Now));

        var result = await service.RunAsync("plushies", TimeSpan.FromHours(120));

        Assert.Equal(IngestionJobStatus.Succeeded, result.Status);
        Assert.Equal(1, result.Candidates);
        Assert.Equal(0, result.Validated);
        Assert.Equal(1, result.Replaced);
        await using var context = factory.CreateDbContext();
        var staleLinks = await context.AffiliateLinks.Where(link => link.ProductId == "stale").ToListAsync();
        Assert.Contains(staleLinks, link => link.Status == AffiliateLinkStatus.Expired);
        Assert.Contains(staleLinks, link => link.Status == AffiliateLinkStatus.Active && link.PromotionUrl.Contains("renewed", StringComparison.Ordinal));
        Assert.Single(await context.AffiliateLinks.Where(link => link.ProductId == "recent").ToListAsync());
        var job = await context.IngestionJobs.SingleAsync();
        Assert.Equal(IngestionJobType.LinkRefresh, job.Type);
        Assert.Equal(1, job.LinksCreatedOrRefreshed);
    }

    [Fact]
    public async Task RunAsync_ValidatesUnchangedLinkWithoutCreatingDuplicate()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedAsync(factory);
        var source = new FakeSource(_ => "https://s.click.aliexpress.com/e/old-stale");
        var service = new AffiliateLinkRenewalService(source, factory, new FixedTimeProvider(Now));

        var result = await service.RunAsync("plushies", TimeSpan.FromHours(120));

        Assert.Equal(1, result.Validated);
        Assert.Equal(0, result.Replaced);
        await using var context = factory.CreateDbContext();
        var link = await context.AffiliateLinks.SingleAsync(item => item.ProductId == "stale");
        Assert.Equal(Now, link.LastValidatedUtc);
        Assert.Equal(AffiliateLinkStatus.Active, link.Status);
    }

    private static async Task SeedAsync(InMemoryFactory factory)
    {
        await using var context = factory.CreateDbContext();
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
            PrimaryColour = "#6f4bc3",
            AccentColour = "#f3c8ef",
            CreatedUtc = Now.AddDays(-30),
            UpdatedUtc = Now.AddDays(-30)
        });
        context.Products.AddRange(Product("stale"), Product("recent"));
        context.AffiliateLinks.AddRange(
            Link(shopId, "stale", "https://www.aliexpress.com/item/stale.html", "https://s.click.aliexpress.com/e/old-stale", Now.AddDays(-10)),
            Link(shopId, "recent", "https://www.aliexpress.com/item/recent.html", "https://s.click.aliexpress.com/e/recent", Now.AddHours(-2)));
        await context.SaveChangesAsync();
    }

    private static ProductRecord Product(string id) => new()
    {
        AliExpressProductId = id,
        Title = $"Product {id}",
        IsEligible = true,
        FirstSeenUtc = Now.AddDays(-30),
        LastSeenUtc = Now,
        LastRefreshedUtc = Now
    };

    private static AffiliateLinkRecord Link(Guid shopId, string productId, string sourceUrl, string promotionUrl, DateTimeOffset validatedUtc) => new()
    {
        Id = Guid.CreateVersion7(),
        ShopId = shopId,
        ProductId = productId,
        SourceUrl = sourceUrl,
        PromotionUrl = promotionUrl,
        TrackingId = "theplushyshop",
        Status = AffiliateLinkStatus.Active,
        GeneratedUtc = validatedUtc,
        LastValidatedUtc = validatedUtc
    };

    private sealed class FakeSource(Func<string, string> promotionUrl) : IAffiliateCatalogueSource
    {
        public Task<AliExpressPage<AliExpressProduct>> SearchAsync(string keywords, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AliExpressPromotionLink>> GenerateLinksAsync(IReadOnlyCollection<string> sourceUrls, string trackingId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AliExpressPromotionLink>>(sourceUrls
                .Select(url => new AliExpressPromotionLink(url, promotionUrl(url), null))
                .ToArray());
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>().UseInMemoryDatabase(databaseName).Options);
    }
}
