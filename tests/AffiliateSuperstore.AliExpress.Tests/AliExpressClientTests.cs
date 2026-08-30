using System.Net;
using System.Text;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AliExpressClientTests
{
    [Fact]
    public async Task GetCategoriesAsync_SendsSignedFormAndRedactsSignatureFromResult()
    {
        var handler = new RecordingHandler(
            "{\"aliexpress_affiliate_category_get_response\":{\"resp_result\":{\"resp_code\":200,\"resp_msg\":\"Call succeeds\"}}}");
        var client = CreateClient(handler);

        var result = await client.GetCategoriesAsync();

        Assert.Equal(HttpStatusCode.OK, result.HttpStatusCode);
        Assert.True(result.IsSuccess);
        Assert.Equal("aliexpress_affiliate_category_get_response", result.ResponseEnvelope);
        Assert.Equal("200", result.PlatformResponseCode);
        Assert.Equal("Call succeeds", result.PlatformResponseMessage);
        Assert.True(result.PlatformSucceeded);
        Assert.NotNull(handler.FormValues);
        Assert.Equal("aliexpress.affiliate.category.get", handler.FormValues["method"]);
        Assert.Equal("12345678", handler.FormValues["app_key"]);
        Assert.Equal("1700000000000", handler.FormValues["timestamp"]);
        Assert.Equal("sha256", handler.FormValues["sign_method"]);
        Assert.Matches("^[0-9A-F]{64}$", handler.FormValues["sign"]);
        Assert.Equal("[redacted]", result.RequestParameters["sign"]);
        Assert.DoesNotContain("local-secret", handler.RawBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchProductsAsync_AddsUkMarketAndTrackingParameters()
    {
        var handler = new RecordingHandler("{}");
        var client = CreateClient(handler);

        await client.SearchProductsAsync(new AliExpressProductSearchRequest
        {
            Keywords = "plush toy",
            PageNumber = 2,
            PageSize = 12,
            Sort = "LAST_VOLUME_DESC"
        });

        Assert.NotNull(handler.FormValues);
        Assert.Equal("plush toy", handler.FormValues["keywords"]);
        Assert.Equal("2", handler.FormValues["page_no"]);
        Assert.Equal("12", handler.FormValues["page_size"]);
        Assert.Equal("GB", handler.FormValues["ship_to_country"]);
        Assert.Equal("GBP", handler.FormValues["target_currency"]);
        Assert.Equal("EN", handler.FormValues["target_language"]);
        Assert.Equal("theplushyshop", handler.FormValues["tracking_id"]);
    }

    [Fact]
    public async Task CallWithoutSecret_FailsBeforeSendingARequest()
    {
        var handler = new RecordingHandler("{}");
        var options = CreateOptions();
        options.AppSecret = string.Empty;
        var client = new AliExpressClient(
            new HttpClient(handler),
            options,
            new AliExpressRequestSigner(),
            new FixedTimeProvider());

        var exception = await Assert.ThrowsAsync<AliExpressConfigurationException>(
            () => client.GetCategoriesAsync());

        Assert.Contains("User Secrets", exception.Message, StringComparison.Ordinal);
        Assert.Null(handler.RawBody);
    }

    [Fact]
    public async Task PromotionInfo_DirectSuccessResponse_IsRecognised()
    {
        var handler = new RecordingHandler("{\"code\":\"200\",\"success\":true,\"result\":{}}");
        var client = CreateClient(handler);

        var result = await client.GetPromotionInfoAsync(new AliExpressPromotionInfoRequest
        {
            ProductIds = ["1005001234567890"]
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.PlatformSucceeded);
        Assert.Equal("200", result.PlatformResponseCode);
        Assert.Equal("UK", handler.FormValues!["ship_to_country"]);
        Assert.Equal("GBP", handler.FormValues["currency"]);
    }

    [Fact]
    public async Task GenerateLinks_RejectsMoreThanFiftyUrlsBeforeSending()
    {
        var handler = new RecordingHandler("{}");
        var client = CreateClient(handler);
        var urls = Enumerable.Range(1, 51)
            .Select(index => $"https://www.aliexpress.com/item/{index}.html")
            .ToArray();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.GenerateAffiliateLinksAsync(new AliExpressLinkGenerateRequest
            {
                SourceUrls = urls
            }));

        Assert.Null(handler.RawBody);
    }

    [Fact]
    public void MethodCatalog_CoversEveryPublishedWrapperMethod()
    {
        var client = CreateClient(new RecordingHandler("{}"));

        Assert.Equal(16, client.Methods.Count);
        Assert.Equal(9, client.Methods.Count(item => item.Permission == AliExpressApiPermission.Standard));
        Assert.Single(client.Methods, item => item.Permission == AliExpressApiPermission.AdditionalApproval);
        Assert.Contains(client.Methods, item => item.IsSystemMethod);
    }

    private static AliExpressClient CreateClient(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            CreateOptions(),
            new AliExpressRequestSigner(),
            new FixedTimeProvider());

    private static AliExpressOptions CreateOptions() => new()
    {
        AppKey = "12345678",
        AppSecret = "local-secret",
        TrackingId = "theplushyshop",
        Gateway = new Uri("https://api-sg.aliexpress.com/sync"),
        ShipToCountry = "GB",
        TargetCurrency = "GBP",
        TargetLanguage = "EN"
    };

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);
    }

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public string? RawBody { get; private set; }

        public Dictionary<string, string>? FormValues { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RawBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            FormValues = RawBody
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2))
                .ToDictionary(
                    part => WebUtility.UrlDecode(part[0]),
                    part => WebUtility.UrlDecode(part.Length == 2 ? part[1] : string.Empty),
                    StringComparer.Ordinal);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
