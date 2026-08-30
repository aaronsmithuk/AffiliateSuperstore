using AffiliateSuperstore.Application.Reporting;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AffiliatePerformanceServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAsync_AttributesClicksOrdersChannelsProductsAndValidCommission()
    {
        var factory = await CreateDatabaseAsync();

        var report = await new AffiliatePerformanceService(factory, new FixedTimeProvider(Now)).GetAsync(30);

        Assert.Equal(2, report.ActiveLinks);
        Assert.Equal(1, report.ActiveLinksClicked);
        Assert.Equal(3, report.Clicks);
        Assert.Equal(2, report.ConvertingClicks);
        Assert.Equal(2, report.AttributedOrders);
        Assert.Equal(1, report.InvalidOrders);
        Assert.Equal(1, report.S2sEvents);
        Assert.Equal(2m / 3m, report.ClickToOrderRate);
        var commission = Assert.Single(report.Commission);
        Assert.Equal("USD", commission.Currency);
        Assert.Equal(1.50m, commission.EstimatedCommission);
        Assert.Equal(1.35m, commission.SettledCommission);
        var channel = Assert.Single(report.Channels);
        Assert.Equal("plushies", channel.Name);
        Assert.Equal(3, channel.Clicks);
        Assert.Equal(2, channel.Orders);
        var product = Assert.Single(report.Products);
        Assert.Equal("Small green dragon plush", product.Name);
        Assert.Equal("product-1", product.Detail);
    }

    private static async Task<InMemoryFactory> CreateDatabaseAsync()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await using var context = factory.CreateDbContext();
        var shopId = Guid.CreateVersion7();
        var clickedLinkId = Guid.CreateVersion7();
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
        context.Products.Add(new ProductRecord
        {
            AliExpressProductId = "product-1",
            Title = "Small green dragon plush",
            FirstSeenUtc = Now.AddDays(-10),
            LastSeenUtc = Now,
            LastRefreshedUtc = Now
        });
        context.AffiliateLinks.AddRange(
            Link(clickedLinkId, shopId, "product-1"),
            Link(Guid.CreateVersion7(), shopId, null));
        context.OutboundClicks.AddRange(
            Click("click-valid", shopId, clickedLinkId, Now.AddDays(-2)),
            Click("click-invalid", shopId, clickedLinkId, Now.AddDays(-1)),
            Click("click-unconverted", shopId, clickedLinkId, Now.AddHours(-2)),
            Click("click-old", shopId, clickedLinkId, Now.AddDays(-31)));
        context.AffiliateOrders.AddRange(
            new AffiliateOrderRecord
            {
                SubOrderId = "order-valid",
                ClickId = "click-valid",
                Status = AliExpressOrderStatuses.CompletedSettlement,
                SettledCurrency = "USD",
                EstimatedPaidCommission = 1.25m,
                EstimatedFinishedCommission = 1.10m,
                EstimatedIncentivePaidCommission = .20m,
                NewBuyerBonusCommission = .05m,
                FirstSeenUtc = Now.AddDays(-2),
                LastSeenUtc = Now
            },
            new AffiliateOrderRecord
            {
                SubOrderId = "order-invalid",
                ClickId = "click-invalid",
                Status = AliExpressOrderStatuses.Invalid,
                SettledCurrency = "USD",
                EstimatedPaidCommission = 99m,
                FirstSeenUtc = Now.AddDays(-1),
                LastSeenUtc = Now
            });
        context.AffiliateS2sEvents.Add(new AffiliateS2sEventRecord
        {
            Id = Guid.CreateVersion7(),
            EventKey = "event-1",
            SubOrderId = "order-valid",
            ReceivedUtc = Now.AddDays(-2),
            ProcessedUtc = Now.AddDays(-2),
            PayloadJson = "{}"
        });
        await context.SaveChangesAsync();
        return factory;
    }

    private static AffiliateLinkRecord Link(Guid id, Guid shopId, string? productId) => new()
    {
        Id = id,
        ShopId = shopId,
        ProductId = productId,
        SourceUrl = "https://www.aliexpress.com/item/test.html",
        PromotionUrl = "https://s.click.aliexpress.com/e/test",
        TrackingId = "theplushyshop",
        Status = AffiliateLinkStatus.Active,
        GeneratedUtc = Now
    };

    private static OutboundClickRecord Click(string id, Guid shopId, Guid linkId, DateTimeOffset clickedUtc) => new()
    {
        ClickId = id,
        ShopId = shopId,
        ProductId = "product-1",
        AffiliateLinkId = linkId,
        TrackingId = "theplushyshop",
        Campaign = "plushies",
        Placement = "product-card",
        ClickedUtc = clickedUtc
    };

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
