using AffiliateSuperstore.Application.Orders;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Web.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AffiliateS2sEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
    private const string VerificationToken = "production-shaped-test-token-32-characters";

    [Fact]
    public async Task HandleAsync_ReturnsNotFoundAndNoStoreWhenDisabled()
    {
        var context = Request("?order_id=order-1");
        var service = Service(new AffiliateS2sOptions(), out _);

        var result = await AffiliateS2sEndpointExtensions.HandleAsync(context.Request, service, default);

        Assert.Equal(StatusCodes.Status404NotFound, StatusCode(result));
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task HandleAsync_ReturnsServiceUnavailableForWeakToken()
    {
        var context = Request("?order_id=order-1&verification_token=placeholder");
        var service = Service(new AffiliateS2sOptions
        {
            Enabled = true,
            VerificationToken = "placeholder"
        }, out _);

        var result = await AffiliateS2sEndpointExtensions.HandleAsync(context.Request, service, default);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, StatusCode(result));
    }

    [Fact]
    public async Task HandleAsync_ReturnsUnauthorizedWithoutExactTokenAndWritesNothing()
    {
        var context = Request("?order_id=order-1&verification_token=wrong-token");
        var service = Service(ConfiguredOptions(), out var factory);

        var result = await AffiliateS2sEndpointExtensions.HandleAsync(context.Request, service, default);

        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(result));
        await using var database = factory.CreateDbContext();
        Assert.Empty(database.AffiliateS2sEvents);
        Assert.Empty(database.AffiliateOrders);
    }

    [Fact]
    public async Task HandleAsync_ReturnsBadRequestForAuthorizedMalformedEvent()
    {
        var context = Request($"?item_id=product-1&verification_token={VerificationToken}");
        var service = Service(ConfiguredOptions(), out var factory);

        var result = await AffiliateS2sEndpointExtensions.HandleAsync(context.Request, service, default);

        Assert.Equal(StatusCodes.Status400BadRequest, StatusCode(result));
        await using var database = factory.CreateDbContext();
        Assert.Empty(database.AffiliateS2sEvents);
        Assert.Empty(database.AffiliateOrders);
    }

    [Fact]
    public async Task HandleAsync_AcceptsThenSuppressesDuplicateWithoutStoringToken()
    {
        var query = "?order_id=order-1&item_id=product-1&commission_fee=1.25&effect_pay_time=2026-09-02%2004%3A00%3A00" +
            $"&verification_token={VerificationToken}";
        var service = Service(ConfiguredOptions(), out var factory);

        var firstContext = Request(query);
        var first = await AffiliateS2sEndpointExtensions.HandleAsync(firstContext.Request, service, default);
        var duplicateContext = Request(query);
        var duplicate = await AffiliateS2sEndpointExtensions.HandleAsync(duplicateContext.Request, service, default);

        Assert.Equal(StatusCodes.Status200OK, StatusCode(first));
        Assert.Equal(StatusCodes.Status200OK, StatusCode(duplicate));
        await using var database = factory.CreateDbContext();
        var inbox = Assert.Single(database.AffiliateS2sEvents);
        Assert.Single(database.AffiliateOrders);
        Assert.DoesNotContain("verification_token", inbox.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(VerificationToken, inbox.PayloadJson, StringComparison.Ordinal);
    }

    private static DefaultHttpContext Request(string query)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.QueryString = new QueryString(query);
        return context;
    }

    private static AffiliateS2sIngestionService Service(
        AffiliateS2sOptions options,
        out InMemoryFactory factory)
    {
        factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        return new AffiliateS2sIngestionService(factory, options, new FixedTimeProvider(Now));
    }

    private static AffiliateS2sOptions ConfiguredOptions() => new()
    {
        Enabled = true,
        VerificationToken = VerificationToken,
        MaximumPayloadCharacters = 8192
    };

    private static int StatusCode(IResult result) =>
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode ?? StatusCodes.Status200OK;

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
