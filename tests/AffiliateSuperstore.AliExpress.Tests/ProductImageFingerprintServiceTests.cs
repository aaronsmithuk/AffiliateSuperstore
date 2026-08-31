using System.Net;
using System.Security.Cryptography;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class ProductImageFingerprintServiceTests
{
    [Fact]
    public async Task RefreshAsync_HashesAllowedImageBytesAndSkipsFreshFingerprint()
    {
        var factory = await SeedAsync("https://ae01.alicdn.com/kf/plush.jpg");
        var bytes = new byte[] { 1, 3, 3, 7, 9 };
        var handler = new StubHandler(_ => ImageResponse(bytes));
        var service = new ProductImageFingerprintService(new HttpClient(handler), factory, TimeProvider.System);

        var first = await service.RefreshAsync("plushies");
        var second = await service.RefreshAsync("plushies");

        Assert.Equal(1, first.FingerprintsCreated);
        Assert.Equal(0, second.ProductsSelected);
        Assert.Equal(1, handler.RequestCount);
        await using var context = factory.CreateDbContext();
        var stored = await context.ProductImageFingerprints.SingleAsync();
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), stored.ContentSha256);
        Assert.Equal(ProductImageFingerprintStatus.Succeeded, stored.Status);
        Assert.Equal(bytes.Length, stored.ContentLength);
    }

    [Fact]
    public async Task RefreshAsync_DoesNotRequestImagesOutsideApprovedCdnHosts()
    {
        var factory = await SeedAsync("https://example.test/plush.jpg");
        var handler = new StubHandler(_ => ImageResponse([1]));
        var service = new ProductImageFingerprintService(new HttpClient(handler), factory, TimeProvider.System);

        var result = await service.RefreshAsync("plushies");

        Assert.Equal(1, result.FailedOrSkipped);
        Assert.Equal(0, handler.RequestCount);
        await using var context = factory.CreateDbContext();
        Assert.Equal(ProductImageFingerprintStatus.Skipped, (await context.ProductImageFingerprints.SingleAsync()).Status);
    }

    [Fact]
    public async Task RefreshAsync_RejectsOversizedResponsesBeforeReadingBody()
    {
        var factory = await SeedAsync("https://ae01.alicdn.com/kf/large.jpg");
        var handler = new StubHandler(_ =>
        {
            var response = ImageResponse([1]);
            response.Content.Headers.ContentLength = ProductImageFingerprintService.MaximumImageBytes + 1;
            return response;
        });
        var service = new ProductImageFingerprintService(new HttpClient(handler), factory, TimeProvider.System);

        var result = await service.RefreshAsync("plushies");

        Assert.Equal(1, result.FailedOrSkipped);
        await using var context = factory.CreateDbContext();
        Assert.Contains("5 MB", (await context.ProductImageFingerprints.SingleAsync()).FailureReason, StringComparison.Ordinal);
    }

    private static HttpResponseMessage ImageResponse(byte[] bytes)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType = new("image/jpeg");
        return response;
    }

    private static async Task<InMemoryFactory> SeedAsync(string imageUrl)
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await using var context = factory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var shopId = Guid.CreateVersion7();
        context.Shops.Add(new ShopRecord
        {
            Id = shopId, Slug = "plushies", DisplayName = "The Plushy Shop", PathPrefix = "/plushies",
            TrackingId = "theplushyshop", DefaultSearchQuery = "plush toy", SeoTitle = "Plush toys",
            SeoDescription = "Curated plush toys", PrimaryColour = "#000000", AccentColour = "#ffffff",
            CreatedUtc = now, UpdatedUtc = now
        });
        context.Products.Add(new ProductRecord
        {
            AliExpressProductId = "image-product", Title = "Highland cow plush", MainImageUrl = imageUrl,
            IsEligible = true, FirstSeenUtc = now, LastSeenUtc = now, LastRefreshedUtc = now
        });
        context.ShopProducts.Add(new ShopProductRecord
        {
            ShopId = shopId, ProductId = "image-product", IsActive = true, FirstIncludedUtc = now, LastIncludedUtc = now
        });
        await context.SaveChangesAsync();
        return factory;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var result = response(request);
            result.RequestMessage = request;
            return Task.FromResult(result);
        }
    }

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>().UseInMemoryDatabase(databaseName).Options);
    }
}
