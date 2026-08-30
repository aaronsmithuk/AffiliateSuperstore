using System.Net;
using System.Text.Json;
using AffiliateSuperstore.Application.Orders;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AffiliateOrderReconciliationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RunAsync_DiscoversAttributesRefreshesAndIdempotentlyUpsertsOrder()
    {
        var factory = await CreateDatabaseAsync();
        var client = new FakeClient { DiscoveryStatus = AliExpressOrderStatuses.PaymentCompleted };
        var service = CreateService(client, factory);

        var result = await service.RunAsync();

        Assert.Equal(IngestionJobStatus.Succeeded, result.Status);
        Assert.Equal(1, result.AttributedOrders);
        Assert.Equal(AliExpressOrderStatuses.All, client.QueriedStatuses);
        Assert.Single(client.RefreshedOrderIds);
        await using (var context = factory.CreateDbContext())
        {
            var order = await context.AffiliateOrders.SingleAsync();
            Assert.Equal(AliExpressOrderStatuses.CompletedSettlement, order.Status);
            Assert.Equal("click-123", order.ClickId);
            Assert.Equal(.07m, order.CommissionRate);
            Assert.Equal(1.11m, order.EstimatedFinishedCommission);
            Assert.Equal(.02m, order.IncentiveCommissionRate);
            Assert.Equal(.35m, order.EstimatedIncentivePaidCommission);
            Assert.NotNull(order.CompletedSettlementUtc);
            Assert.NotNull((await context.OutboundClicks.SingleAsync()).ConvertedUtc);
        }

        client.DiscoveryStatus = AliExpressOrderStatuses.CompletedSettlement;
        var second = await service.RunAsync();

        Assert.Equal(IngestionJobStatus.Succeeded, second.Status);
        await using var verification = factory.CreateDbContext();
        Assert.Equal(1, await verification.AffiliateOrders.CountAsync());
        Assert.Equal(2, await verification.IngestionJobs.CountAsync());
    }

    [Fact]
    public async Task RunAsync_RecordsApiFailureForOperationalRetry()
    {
        var factory = await CreateDatabaseAsync();
        var client = new FakeClient { FailQueries = true };

        var result = await CreateService(client, factory).RunAsync();

        Assert.Equal(IngestionJobStatus.Failed, result.Status);
        Assert.Contains("simulated API outage", result.Error, StringComparison.Ordinal);
        await using var context = factory.CreateDbContext();
        var job = await context.IngestionJobs.SingleAsync();
        Assert.Equal(IngestionJobStatus.Failed, job.Status);
        Assert.Contains("simulated API outage", job.ErrorSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void Planner_UsesShortFailureRetryAndNormalSuccessInterval()
    {
        var options = Options();

        Assert.True(OrderReconciliationPlanner.IsDue(IngestionJobStatus.Failed, Now.AddMinutes(-16), Now, options));
        Assert.False(OrderReconciliationPlanner.IsDue(IngestionJobStatus.Succeeded, Now.AddMinutes(-59), Now, options));
        Assert.True(OrderReconciliationPlanner.IsDue(IngestionJobStatus.Succeeded, Now.AddMinutes(-60), Now, options));
    }

    private static AffiliateOrderReconciliationService CreateService(
        IAliExpressClient client,
        IDbContextFactory<AffiliateSuperstoreDbContext> factory) =>
        new(client, factory, Options(), new FixedTimeProvider(Now));

    private static OrderReconciliationOptions Options() => new()
    {
        InitialLookbackDays = 180,
        IncrementalLookbackHours = 48,
        PageSize = 50,
        MaximumPagesPerStatus = 10,
        OpenOrderBatchSize = 50,
        RefreshEveryMinutes = 60,
        FailureRetryMinutes = 15
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
            ClickId = "click-123",
            ShopId = shopId,
            TrackingId = "theplushyshop",
            Campaign = "plushies",
            Placement = "product-card",
            ClickedUtc = Now.AddDays(-1)
        });
        await context.SaveChangesAsync();
        return factory;
    }

    private sealed class FakeClient : IAliExpressClient
    {
        public string DiscoveryStatus { get; set; } = AliExpressOrderStatuses.PaymentCompleted;
        public bool FailQueries { get; set; }
        public List<string> QueriedStatuses { get; } = [];
        public List<string> RefreshedOrderIds { get; } = [];
        public IReadOnlyList<AliExpressApiMethodDescriptor> Methods => [];

        public Task<AliExpressApiCallResult> ListOrdersByIndexAsync(AliExpressOrderListByIndexRequest request, CancellationToken cancellationToken = default)
        {
            QueriedStatuses.Add(request.Status);
            if (FailQueries) return Task.FromResult(Failure("aliexpress.affiliate.order.listbyindex"));
            return Task.FromResult(request.Status == DiscoveryStatus
                ? Success("aliexpress.affiliate.order.listbyindex", OrderResponse(request.Status, request.Status == AliExpressOrderStatuses.CompletedSettlement))
                : Empty("aliexpress.affiliate.order.listbyindex"));
        }

        public Task<AliExpressApiCallResult> GetOrdersAsync(AliExpressOrderGetRequest request, CancellationToken cancellationToken = default)
        {
            RefreshedOrderIds.AddRange(request.OrderIds);
            return Task.FromResult(Success("aliexpress.affiliate.order.get", OrderResponse(AliExpressOrderStatuses.CompletedSettlement, true)));
        }

        private static string OrderResponse(string status, bool settled) => JsonSerializer.Serialize(new
        {
            resp_result = new
            {
                resp_code = "200",
                result = new
                {
                    current_record_count = "1",
                    min_query_index_id = "first",
                    max_query_index_id = "last",
                    orders = new[]
                    {
                        new
                        {
                            sub_order_id = "order-1",
                            order_id = "parent-1",
                            order_status = status,
                            product_id = "product-1",
                            product_title = "Small green dragon plush",
                            tracking_id = "theplushyshop",
                            custom_parameters = "{\"cn\":\"plushies\",\"dp\":\"click-123\"}",
                            commission_rate = "7%",
                            estimated_paid_commission = "1.25",
                            estimated_finished_commission = settled ? "1.11" : "",
                            incentive_commission_rate = "2%",
                            estimated_incentive_paid_commission = ".35",
                            new_buyer_bonus_commission = ".50",
                            is_new_buyer = "Y",
                            order_platform = "affiliate_platform",
                            order_type = "global",
                            paid_amount = "17.85",
                            finished_amount = settled ? "17.85" : "",
                            settled_currency = "USD",
                            paid_time = "2026-08-29 12:00:00",
                            finished_time = settled ? "2026-08-30 09:00:00" : "",
                            completed_settlement_time = settled ? "2026-08-30 10:00:00" : "",
                            ship_to_country = "GB",
                            is_affiliate_product = "Y",
                            is_hot_product = "N"
                        }
                    }
                }
            }
        });

        private static AliExpressApiCallResult Success(string method, string raw) => new(
            method, Now, TimeSpan.FromMilliseconds(10), HttpStatusCode.OK,
            new Dictionary<string, string>(), raw, raw, null, "200", "success", true);

        private static AliExpressApiCallResult Empty(string method) => new(
            method, Now, TimeSpan.FromMilliseconds(10), HttpStatusCode.OK,
            new Dictionary<string, string>(), "{}", "{}", null, "405", "The result is empty", false);

        private static AliExpressApiCallResult Failure(string method) => new(
            method, Now, TimeSpan.FromMilliseconds(10), HttpStatusCode.ServiceUnavailable,
            new Dictionary<string, string>(), "{}", "{}", "error_response", "500", "simulated API outage", false);

        public Task<AliExpressApiCallResult> GetCategoriesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AliExpressApiCallResult> GetFeaturedPromotionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AliExpressApiCallResult> GetFeaturedPromotionProductsAsync(AliExpressFeaturedPromotionProductsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AliExpressApiCallResult> SearchProductsAsync(AliExpressProductSearchRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AliExpressApiCallResult> GenerateAffiliateLinkAsync(string sourceUrl, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AliExpressApiCallResult> GenerateAffiliateLinksAsync(AliExpressLinkGenerateRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AliExpressApiCallResult> GetProductDetailsAsync(AliExpressProductDetailRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AliExpressApiCallResult> GetPromotionInfoAsync(AliExpressPromotionInfoRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AliExpressApiCallResult> QueryHotProductsAsync(AliExpressProductSearchRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AliExpressApiCallResult> DownloadHotProductsAsync(AliExpressHotProductDownloadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AliExpressApiCallResult> GetProductShippingAsync(AliExpressProductShippingRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AliExpressApiCallResult> GetSkuDetailsAsync(AliExpressSkuDetailRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AliExpressApiCallResult> SmartMatchAsync(AliExpressSmartMatchRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AliExpressApiCallResult> ListOrdersAsync(AliExpressOrderListRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AliExpressApiCallResult> GetMerchantLicenseAsync(AliExpressMerchantLicenseRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
