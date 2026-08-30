using AffiliateSuperstore.AliExpress;
using Microsoft.Extensions.Configuration;

var repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
var configuration = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(repositoryRoot, "src", "AffiliateSuperstore.Web", "appsettings.json"), optional: false)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var options = new AliExpressOptions();
configuration.GetSection(AliExpressOptions.SectionName).Bind(options);

using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AffiliateSuperstore-SmokeTest/0.2");
var client = new AliExpressClient(httpClient, options, new AliExpressRequestSigner());

Console.WriteLine("AliExpress Affiliate API live smoke test");
Console.WriteLine($"Market: {options.ShipToCountry} / {options.TargetCurrency} / {options.TargetLanguage}");
Console.WriteLine($"Tracking ID configured: {options.HasTrackingId}");
Console.WriteLine($"Published wrapper methods: {client.Methods.Count}");
Console.WriteLine();

var requiredPassed = true;
var categories = await RunAsync("Categories", () => client.GetCategoriesAsync(), required: true);
requiredPassed &= categories.Passed;
if (categories.Result is not null)
{
    var items = AliExpressResponseReader.ReadCategories(categories.Result.RawResponse);
    Console.WriteLine($"  Parsed categories: {items.Count:N0}; first: {items.FirstOrDefault()?.Name ?? "[none]"}");
}

var search = await RunAsync(
    "Product search",
    () => client.SearchProductsAsync(new AliExpressProductSearchRequest
    {
        Keywords = "plush toy",
        PageNumber = 1,
        PageSize = 5,
        Sort = "LAST_VOLUME_DESC"
    }),
    required: true);
requiredPassed &= search.Passed;

AliExpressProduct? firstProduct = null;
if (search.Result is not null)
{
    var page = AliExpressResponseReader.ReadProducts(search.Result.RawResponse);
    firstProduct = page.Items.FirstOrDefault();
    Console.WriteLine($"  Parsed products: {page.Items.Count:N0}; total: {page.TotalRecords?.ToString("N0") ?? "[not supplied]"}");
    Console.WriteLine($"  First product: {firstProduct?.Title ?? "[none]"}");
}

if (firstProduct is not null)
{
    var detail = await RunAsync(
        "Product details",
        () => client.GetProductDetailsAsync(new AliExpressProductDetailRequest
        {
            ProductIds = [firstProduct.ProductId]
        }),
        required: true);
    requiredPassed &= detail.Passed;

    var sourceUrl = firstProduct.ProductDetailUrl ?? $"https://www.aliexpress.com/item/{firstProduct.ProductId}.html";
    var links = await RunAsync(
        "Affiliate link generation",
        () => client.GenerateAffiliateLinkAsync(sourceUrl),
        required: true);
    requiredPassed &= links.Passed;
    if (links.Result is not null)
    {
        var link = AliExpressResponseReader.ReadPromotionLinks(links.Result.RawResponse).FirstOrDefault();
        Console.WriteLine($"  Parsed tracked link: {link?.PromotionUrl ?? "[none]"}");
    }

    await RunAsync(
        "Promotion/coupon information",
        () => client.GetPromotionInfoAsync(new AliExpressPromotionInfoRequest
        {
            ProductIds = [firstProduct.ProductId]
        }),
        required: false);
}

var promotions = await RunAsync(
    "Featured promotions",
    () => client.GetFeaturedPromotionsAsync(),
    required: false);
if (promotions.Result is not null)
{
    var items = AliExpressResponseReader.ReadFeaturedPromotions(promotions.Result.RawResponse);
    Console.WriteLine($"  Parsed promotions: {items.Count:N0}; first: {items.FirstOrDefault()?.Name ?? "[none]"}");
    if (items.FirstOrDefault() is { } promotion)
    {
        await RunAsync(
            "Featured promotion products",
            () => client.GetFeaturedPromotionProductsAsync(new AliExpressFeaturedPromotionProductsRequest
            {
                PromotionName = promotion.Name,
                PageNumber = 1,
                PageSize = 5
            }),
            required: false);
    }
}

var endTime = DateTime.Today.AddDays(1);
var startTime = endTime.AddDays(-7);
await RunAsync(
    "Recent order list",
    () => client.ListOrdersAsync(new AliExpressOrderListRequest
    {
        StartTimePacific = startTime,
        EndTimePacific = endTime,
        PageNumber = 1,
        PageSize = 10
    }),
    required: false);

Console.WriteLine();
Console.WriteLine("Permission-aware coverage");
foreach (var group in client.Methods.GroupBy(item => item.Permission))
{
    var enabled = group.Key switch
    {
        AliExpressApiPermission.Standard => true,
        AliExpressApiPermission.AdditionalApproval => options.PromotionInfoApiEnabled,
        AliExpressApiPermission.Advanced => options.AdvancedApiEnabled,
        AliExpressApiPermission.SkuDimension => options.SkuDimensionApiEnabled,
        AliExpressApiPermission.SystemTool => options.SystemToolEnabled,
        _ => false
    };
    Console.WriteLine($"  {group.Key}: {group.Count()} method(s), {(enabled ? "enabled" : "not granted/configured")}");
}

Console.WriteLine();
Console.WriteLine(requiredPassed ? "All required live smoke tests passed." : "One or more required live smoke tests failed.");
return requiredPassed ? 0 : 1;

static async Task<SmokeResult> RunAsync(
    string name,
    Func<Task<AliExpressApiCallResult>> operation,
    bool required)
{
    Console.WriteLine($"{name}{(required ? string.Empty : " (informational)")}...");

    try
    {
        var result = await operation();
        Console.WriteLine($"  HTTP {(int)result.HttpStatusCode}; AliExpress {result.PlatformResponseCode ?? "[no code]"}; {result.Duration.TotalMilliseconds:N0} ms");
        if (!string.IsNullOrWhiteSpace(result.PlatformResponseMessage)) Console.WriteLine($"  Message: {result.PlatformResponseMessage}");

        if (result.IsSuccess)
        {
            Console.WriteLine("  PASS");
            return new SmokeResult(true, result);
        }

        Console.WriteLine(required ? "  FAIL" : "  NOT AVAILABLE / NO RESULT");
        Console.WriteLine(Indent(Truncate(result.FormattedResponse, 1_500), "  "));
        return new SmokeResult(!required, result);
    }
    catch (Exception exception)
    {
        Console.WriteLine($"  {(required ? "FAIL" : "NOT AVAILABLE")}: {exception.GetType().Name}: {exception.Message}");
        return new SmokeResult(!required, null);
    }
}

static string FindRepositoryRoot(string startPath)
{
    var directory = new DirectoryInfo(startPath);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "AffiliateSuperstore.slnx"))) return directory.FullName;
        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate AffiliateSuperstore.slnx.");
}

static string Truncate(string value, int maximumLength) => value.Length <= maximumLength ? value : value[..maximumLength] + Environment.NewLine + "[truncated]";
static string Indent(string value, string prefix) => string.Join(Environment.NewLine, value.Split(Environment.NewLine).Select(line => prefix + line));
internal sealed record SmokeResult(bool Passed, AliExpressApiCallResult? Result);
