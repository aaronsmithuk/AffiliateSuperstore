using AffiliateSuperstore.Application.Orders;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AffiliateS2sIngestionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 20, 0, 0, TimeSpan.Zero);
    private const string VerificationToken = "test-fixed-secret-with-at-least-32-chars";

    [Fact]
    public async Task IngestAsync_AttributesPaidOrderStoresAllowListedEventAndSuppressesDuplicate()
    {
        var factory = await CreateDatabaseAsync();
        var service = CreateService(factory);
        var payload = Payload();

        var first = await service.IngestAsync(payload);
        var second = await service.IngestAsync(payload);

        Assert.Equal(AffiliateS2sDisposition.Accepted, first.Disposition);
        Assert.Equal(AffiliateS2sDisposition.Duplicate, second.Disposition);
        Assert.True(service.IsAuthorized(VerificationToken));
        Assert.False(service.IsAuthorized("wrong-secret"));
        await using var context = factory.CreateDbContext();
        var order = await context.AffiliateOrders.SingleAsync();
        Assert.Equal(AliExpressOrderStatuses.PaymentCompleted, order.Status);
        Assert.Equal("click-s2s", order.ClickId);
        Assert.Equal(.07m, order.CommissionRate);
        Assert.Equal(1.25m, order.EstimatedPaidCommission);
        Assert.Equal(.02m, order.IncentiveCommissionRate);
        Assert.Equal(.35m, order.EstimatedIncentivePaidCommission);
        Assert.True(order.IsNewBuyer);
        Assert.Equal(new DateTimeOffset(2026, 8, 30, 19, 0, 0, TimeSpan.Zero), order.PaidUtc);
        Assert.NotNull((await context.OutboundClicks.SingleAsync()).ConvertedUtc);
        var inbox = await context.AffiliateS2sEvents.SingleAsync();
        Assert.DoesNotContain("verification_token", inbox.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unexpected_personal_field", inbox.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IngestAsync_DoesNotDowngradeTerminalReconciledOrder()
    {
        var factory = await CreateDatabaseAsync();
        await using (var setup = factory.CreateDbContext())
        {
            setup.AffiliateOrders.Add(new AffiliateOrderRecord
            {
                SubOrderId = "s2s-order-1",
                Status = AliExpressOrderStatuses.CompletedSettlement,
                FirstSeenUtc = Now.AddDays(-5),
                LastSeenUtc = Now.AddDays(-1),
                CompletedSettlementUtc = Now.AddDays(-1)
            });
            await setup.SaveChangesAsync();
        }

        var result = await CreateService(factory).IngestAsync(Payload());

        Assert.Equal(AffiliateS2sDisposition.Accepted, result.Disposition);
        await using var context = factory.CreateDbContext();
        Assert.Equal(AliExpressOrderStatuses.CompletedSettlement, (await context.AffiliateOrders.SingleAsync()).Status);
    }

    [Fact]
    public async Task IngestAsync_RejectsPayloadWithoutSubOrderId()
    {
        var factory = await CreateDatabaseAsync();

        var result = await CreateService(factory).IngestAsync(new Dictionary<string, string>());

        Assert.Equal(AffiliateS2sDisposition.Rejected, result.Disposition);
        Assert.Contains("order_id", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(511)]
    [InlineData(65537)]
    public void Options_RejectInvalidPayloadLimit(int maximumPayloadCharacters)
    {
        var options = new AffiliateS2sOptions
        {
            Enabled = true,
            VerificationToken = VerificationToken,
            MaximumPayloadCharacters = maximumPayloadCharacters
        };

        var error = Assert.Throws<InvalidOperationException>(() => AffiliateS2sOptions.Validate(options));

        Assert.Contains("MaximumPayloadCharacters", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(513)]
    public void Options_RejectInvalidVerificationTokenLength(int tokenLength)
    {
        var options = new AffiliateS2sOptions
        {
            Enabled = true,
            VerificationToken = new string('x', tokenLength)
        };

        var error = Assert.Throws<InvalidOperationException>(() => AffiliateS2sOptions.Validate(options));

        Assert.Contains("VerificationToken", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IsConfigured_RejectsWeakTokenWhenEnabled()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        var service = new AffiliateS2sIngestionService(factory, new AffiliateS2sOptions
        {
            Enabled = true,
            VerificationToken = "placeholder"
        }, new FixedTimeProvider(Now));

        Assert.True(service.IsEnabled);
        Assert.False(service.IsConfigured);
        Assert.False(service.IsAuthorized("placeholder"));
    }

    private static AffiliateS2sIngestionService CreateService(IDbContextFactory<AffiliateSuperstoreDbContext> factory) =>
        new(factory, new AffiliateS2sOptions
        {
            Enabled = true,
            VerificationToken = VerificationToken,
            MaximumPayloadCharacters = 8192
        }, new FixedTimeProvider(Now));

    private static Dictionary<string, string> Payload() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["order_id"] = "s2s-order-1",
        ["item_id"] = "product-1",
        ["effect_pay_time"] = "2026-08-30 12:00:00",
        ["country"] = "GB",
        ["order_amount"] = "17.85",
        ["currency"] = "USD",
        ["commission_rate"] = "7%",
        ["commission_fee"] = "1.25",
        ["incentive_commission_rate"] = "2%",
        ["incentive_commission"] = ".35",
        ["is_new_buyer"] = "Y",
        ["new_buyer_bonus"] = ".50",
        ["clickid"] = "click-s2s",
        ["tracking_id"] = "theplushyshop",
        ["is_affiliate_item"] = "Y",
        ["is_hot_product"] = "N",
        ["unexpected_personal_field"] = "must-not-be-stored"
    };

    private static async Task<InMemoryFactory> CreateDatabaseAsync()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
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
            PrimaryColour = "#000000",
            AccentColour = "#ffffff",
            CreatedUtc = Now,
            UpdatedUtc = Now
        });
        context.OutboundClicks.Add(new OutboundClickRecord
        {
            ClickId = "click-s2s",
            ShopId = shopId,
            TrackingId = "theplushyshop",
            Campaign = "plushies",
            Placement = "product-card",
            ClickedUtc = Now.AddHours(-2)
        });
        await context.SaveChangesAsync();
        return factory;
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
