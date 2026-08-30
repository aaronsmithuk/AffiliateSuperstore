using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace AffiliateSuperstore.AliExpress;

public sealed class AliExpressClient : IAliExpressClient
{
    private const string CategoryMethod = "aliexpress.affiliate.category.get";
    private const string FeaturedPromotionsMethod = "aliexpress.affiliate.featuredpromo.get";
    private const string FeaturedPromotionProductsMethod = "aliexpress.affiliate.featuredpromo.products.get";
    private const string HotProductDownloadMethod = "aliexpress.affiliate.hotproduct.download";
    private const string HotProductQueryMethod = "aliexpress.affiliate.hotproduct.query";
    private const string LinkGenerateMethod = "aliexpress.affiliate.link.generate";
    private const string MerchantLicenseMethod = "/aliexpress/xinghe/merchant/license/get";
    private const string OrderGetMethod = "aliexpress.affiliate.order.get";
    private const string OrderListMethod = "aliexpress.affiliate.order.list";
    private const string OrderListByIndexMethod = "aliexpress.affiliate.order.listbyindex";
    private const string ProductDetailMethod = "aliexpress.affiliate.productdetail.get";
    private const string ProductQueryMethod = "aliexpress.affiliate.product.query";
    private const string ProductShippingMethod = "aliexpress.affiliate.product.shipping.get";
    private const string PromotionInfoMethod = "aliexpress.affiliate.promotion.info.get";
    private const string SkuDetailMethod = "aliexpress.affiliate.product.sku.detail.get";
    private const string SmartMatchMethod = "aliexpress.affiliate.product.smartmatch";
    private const string PacificDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly HttpClient _httpClient;
    private readonly AliExpressOptions _options;
    private readonly AliExpressRequestSigner _signer;
    private readonly TimeProvider _timeProvider;

    public AliExpressClient(
        HttpClient httpClient,
        AliExpressOptions options,
        AliExpressRequestSigner signer,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient;
        _options = options;
        _signer = signer;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IReadOnlyList<AliExpressApiMethodDescriptor> Methods => AliExpressApiMethodCatalog.All;

    public Task<AliExpressApiCallResult> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        ExecuteBusinessAsync(CategoryMethod, new Dictionary<string, string?>(), cancellationToken);

    public Task<AliExpressApiCallResult> GetFeaturedPromotionsAsync(CancellationToken cancellationToken = default) =>
        ExecuteBusinessAsync(FeaturedPromotionsMethod, new Dictionary<string, string?>(), cancellationToken);

    public Task<AliExpressApiCallResult> GetFeaturedPromotionProductsAsync(
        AliExpressFeaturedPromotionProductsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePagination(request.PageNumber, request.PageSize);

        var parameters = CreateMarketParameters(useCountryParameter: true);
        parameters["promotion_name"] = NullIfWhiteSpace(request.PromotionName);
        parameters["category_id"] = NullIfWhiteSpace(request.CategoryId);
        parameters["fields"] = NullIfWhiteSpace(request.Fields);
        parameters["page_no"] = Invariant(request.PageNumber);
        parameters["page_size"] = Invariant(request.PageSize);
        parameters["sort"] = NullIfWhiteSpace(request.Sort);
        parameters["promotion_start_time"] = FormatPacificTime(request.PromotionStartTimePacific);
        parameters["promotion_end_time"] = FormatPacificTime(request.PromotionEndTimePacific);

        return ExecuteBusinessAsync(FeaturedPromotionProductsMethod, parameters, cancellationToken);
    }

    public Task<AliExpressApiCallResult> SearchProductsAsync(
        AliExpressProductSearchRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteProductQueryAsync(ProductQueryMethod, request, cancellationToken);

    public Task<AliExpressApiCallResult> QueryHotProductsAsync(
        AliExpressProductSearchRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteProductQueryAsync(HotProductQueryMethod, request, cancellationToken);

    public Task<AliExpressApiCallResult> DownloadHotProductsAsync(
        AliExpressHotProductDownloadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CategoryId);
        ValidatePagination(request.PageNumber, request.PageSize);

        var parameters = CreateMarketParameters(useCountryParameter: true);
        parameters["category_id"] = request.CategoryId.Trim();
        parameters["fields"] = NullIfWhiteSpace(request.Fields);
        parameters["locale_site"] = NullIfWhiteSpace(request.LocaleSite);
        parameters["page_no"] = Invariant(request.PageNumber);
        parameters["page_size"] = Invariant(request.PageSize);

        return ExecuteBusinessAsync(HotProductDownloadMethod, parameters, cancellationToken);
    }

    public Task<AliExpressApiCallResult> GenerateAffiliateLinkAsync(
        string sourceUrl,
        CancellationToken cancellationToken = default) =>
        GenerateAffiliateLinksAsync(
            new AliExpressLinkGenerateRequest { SourceUrls = [sourceUrl] },
            cancellationToken);

    public Task<AliExpressApiCallResult> GenerateAffiliateLinksAsync(
        AliExpressLinkGenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SourceUrls.Count is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Supply between 1 and 50 source URLs.");
        }

        var sourceUrls = request.SourceUrls.Select(ValidateAliExpressUrl).ToArray();
        var trackingId = NullIfWhiteSpace(request.TrackingId) ?? NullIfWhiteSpace(_options.TrackingId);

        if (trackingId is null)
        {
            throw new AliExpressConfigurationException(
                "A Tracking ID is required to generate affiliate links.");
        }

        if (request.PromotionLinkType is not (0 or 2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Promotion link type must be 0 (standard) or 2 (hot product).");
        }

        var parameters = new Dictionary<string, string?>
        {
            ["promotion_link_type"] = Invariant(request.PromotionLinkType),
            ["source_values"] = string.Join(',', sourceUrls),
            ["tracking_id"] = trackingId,
            ["ship_to_country"] = _options.ShipToCountry
        };

        return ExecuteBusinessAsync(LinkGenerateMethod, parameters, cancellationToken);
    }

    public Task<AliExpressApiCallResult> GetProductDetailsAsync(
        AliExpressProductDetailRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var productIds = JoinRequiredValues(request.ProductIds, 50, "product IDs");
        var parameters = CreateMarketParameters(useCountryParameter: true);
        parameters["product_ids"] = productIds;
        parameters["fields"] = NullIfWhiteSpace(request.Fields);

        return ExecuteBusinessAsync(ProductDetailMethod, parameters, cancellationToken);
    }

    public Task<AliExpressApiCallResult> GetPromotionInfoAsync(
        AliExpressPromotionInfoRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var productIds = JoinRequiredValues(request.ProductIds, 10, "product IDs");
        var parameters = new Dictionary<string, string?>
        {
            ["currency"] = _options.TargetCurrency,
            ["target_language"] = _options.TargetLanguage,
            ["product_id"] = productIds,
            ["ship_to_country"] = ToPromotionCountry(_options.ShipToCountry)
        };

        return ExecuteBusinessAsync(PromotionInfoMethod, parameters, cancellationToken);
    }

    public Task<AliExpressApiCallResult> GetProductShippingAsync(
        AliExpressProductShippingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProductId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SkuId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetSalePrice);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaxRate);

        var parameters = new Dictionary<string, string?>
        {
            ["product_id"] = request.ProductId.Trim(),
            ["sku_id"] = request.SkuId.Trim(),
            ["ship_to_country"] = _options.ShipToCountry,
            ["target_currency"] = _options.TargetCurrency,
            ["target_sale_price"] = request.TargetSalePrice.Trim(),
            ["target_language"] = _options.TargetLanguage,
            ["tax_rate"] = request.TaxRate.Trim()
        };

        return ExecuteBusinessAsync(ProductShippingMethod, parameters, cancellationToken);
    }

    public Task<AliExpressApiCallResult> GetSkuDetailsAsync(
        AliExpressSkuDetailRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProductId);

        if (request.SkuIds.Count > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Supply no more than 20 SKU IDs.");
        }

        var parameters = new Dictionary<string, string?>
        {
            ["product_id"] = request.ProductId.Trim(),
            ["sku_ids"] = JoinOptionalValues(request.SkuIds),
            ["ship_to_country"] = _options.ShipToCountry,
            ["target_currency"] = _options.TargetCurrency,
            ["target_language"] = _options.TargetLanguage,
            ["need_deliver_info"] = request.IncludeDeliveryInformation ? "Yes" : "No"
        };

        return ExecuteBusinessAsync(SkuDetailMethod, parameters, cancellationToken);
    }

    public Task<AliExpressApiCallResult> SmartMatchAsync(
        AliExpressSmartMatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Page number must be at least 1.");
        }

        if (string.IsNullOrWhiteSpace(request.DeviceId) &&
            string.IsNullOrWhiteSpace(request.ProductId) &&
            string.IsNullOrWhiteSpace(request.Keywords))
        {
            throw new ArgumentException(
                "Smart match requires a device ID, product ID or keywords.",
                nameof(request));
        }

        var parameters = CreateMarketParameters(useCountryParameter: true);
        parameters["page_no"] = Invariant(request.PageNumber);
        parameters["device_id"] = NullIfWhiteSpace(request.DeviceId);
        parameters["product_id"] = NullIfWhiteSpace(request.ProductId);
        parameters["keywords"] = NullIfWhiteSpace(request.Keywords);
        parameters["site"] = NullIfWhiteSpace(request.Site);
        parameters["user"] = NullIfWhiteSpace(request.User);
        parameters["app"] = NullIfWhiteSpace(request.App);
        parameters["device"] = NullIfWhiteSpace(request.Device);
        parameters["fields"] = NullIfWhiteSpace(request.Fields);

        return ExecuteBusinessAsync(SmartMatchMethod, parameters, cancellationToken);
    }

    public Task<AliExpressApiCallResult> GetOrdersAsync(
        AliExpressOrderGetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parameters = new Dictionary<string, string?>
        {
            ["order_ids"] = JoinRequiredValues(request.OrderIds, 50, "order IDs"),
            ["fields"] = NullIfWhiteSpace(request.Fields)
        };

        return ExecuteBusinessAsync(OrderGetMethod, parameters, cancellationToken);
    }

    public Task<AliExpressApiCallResult> ListOrdersAsync(
        AliExpressOrderListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateOrderQuery(request.StartTimePacific, request.EndTimePacific, request.Status);
        ValidatePagination(request.PageNumber, request.PageSize);

        var parameters = CreateOrderParameters(
            request.StartTimePacific,
            request.EndTimePacific,
            request.Status,
            request.TimeType,
            request.Fields,
            request.PageSize);
        parameters["locale_site"] = NullIfWhiteSpace(request.LocaleSite);
        parameters["page_no"] = Invariant(request.PageNumber);

        return ExecuteBusinessAsync(OrderListMethod, parameters, cancellationToken);
    }

    public Task<AliExpressApiCallResult> ListOrdersByIndexAsync(
        AliExpressOrderListByIndexRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateOrderQuery(request.StartTimePacific, request.EndTimePacific, request.Status);

        if (request.PageSize is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Page size must be between 1 and 50.");
        }

        var parameters = CreateOrderParameters(
            request.StartTimePacific,
            request.EndTimePacific,
            request.Status,
            request.TimeType,
            request.Fields,
            request.PageSize);
        parameters["start_query_index_id"] = NullIfWhiteSpace(request.StartQueryIndexId);

        return ExecuteBusinessAsync(OrderListByIndexMethod, parameters, cancellationToken);
    }

    public Task<AliExpressApiCallResult> GetMerchantLicenseAsync(
        AliExpressMerchantLicenseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SellerAdminSequence);

        var parameters = new Dictionary<string, string?>
        {
            ["param0"] = JsonSerializer.Serialize(new
            {
                sellerAdminSeq = request.SellerAdminSequence.Trim(),
                channel = NullIfWhiteSpace(request.Channel)
            })
        };

        return ExecuteSystemAsync(MerchantLicenseMethod, parameters, cancellationToken);
    }

    private Task<AliExpressApiCallResult> ExecuteProductQueryAsync(
        string method,
        AliExpressProductSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePagination(request.PageNumber, request.PageSize);

        if (request.MinimumSalePriceInCents is < 0 || request.MaximumSalePriceInCents is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Price filters cannot be negative.");
        }

        if (request.MinimumSalePriceInCents > request.MaximumSalePriceInCents)
        {
            throw new ArgumentException("Minimum sale price cannot exceed maximum sale price.", nameof(request));
        }

        if (request.DeliveryDays is not null and not (3 or 5 or 7 or 10))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Delivery days must be 3, 5, 7 or 10.");
        }

        var parameters = CreateMarketParameters();
        parameters["keywords"] = NullIfWhiteSpace(request.Keywords);
        parameters["category_ids"] = NullIfWhiteSpace(request.CategoryIds);
        parameters["fields"] = NullIfWhiteSpace(request.Fields);
        parameters["min_sale_price"] = Invariant(request.MinimumSalePriceInCents);
        parameters["max_sale_price"] = Invariant(request.MaximumSalePriceInCents);
        parameters["page_no"] = Invariant(request.PageNumber);
        parameters["page_size"] = Invariant(request.PageSize);
        parameters["sort"] = NullIfWhiteSpace(request.Sort);
        parameters["platform_product_type"] = NullIfWhiteSpace(request.PlatformProductType);
        parameters["promotion_name"] = NullIfWhiteSpace(request.PromotionName);
        parameters["delivery_days"] = Invariant(request.DeliveryDays);

        return ExecuteBusinessAsync(method, parameters, cancellationToken);
    }

    private async Task<AliExpressApiCallResult> ExecuteBusinessAsync(
        string method,
        IReadOnlyDictionary<string, string?> businessParameters,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var startedAt = _timeProvider.GetUtcNow();
        var parameters = AddCommonParameters(businessParameters, startedAt);
        parameters["method"] = method;
        parameters["sign"] = _signer.Sign(parameters, _options.AppSecret);

        return await SendAsync(method, _options.Gateway, parameters, startedAt, cancellationToken);
    }

    private async Task<AliExpressApiCallResult> ExecuteSystemAsync(
        string apiPath,
        IReadOnlyDictionary<string, string?> businessParameters,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var startedAt = _timeProvider.GetUtcNow();
        var parameters = AddCommonParameters(businessParameters, startedAt);
        parameters["sign"] = _signer.SignSystemRequest(apiPath, parameters, _options.AppSecret);
        var endpoint = new Uri(_options.SystemGateway.ToString().TrimEnd('/') + apiPath);

        return await SendAsync(apiPath, endpoint, parameters, startedAt, cancellationToken);
    }

    private async Task<AliExpressApiCallResult> SendAsync(
        string method,
        Uri endpoint,
        IReadOnlyDictionary<string, string?> parameters,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(
                parameters
                    .Where(parameter => !string.IsNullOrEmpty(parameter.Value))
                    .Select(parameter =>
                        new KeyValuePair<string, string>(parameter.Key, parameter.Value!)))
        };

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        var responseMetadata = InspectResponse(rawResponse);

        return new AliExpressApiCallResult(
            method,
            startedAt,
            stopwatch.Elapsed,
            response.StatusCode,
            CreateDisplayParameters(parameters),
            rawResponse,
            FormatJson(rawResponse),
            responseMetadata.Envelope,
            responseMetadata.Code,
            responseMetadata.Message,
            responseMetadata.Success);
    }

    private Dictionary<string, string?> CreateMarketParameters(bool useCountryParameter = false) =>
        new(StringComparer.Ordinal)
        {
            ["target_currency"] = _options.TargetCurrency,
            ["target_language"] = _options.TargetLanguage,
            ["tracking_id"] = NullIfWhiteSpace(_options.TrackingId),
            [useCountryParameter ? "country" : "ship_to_country"] = _options.ShipToCountry
        };

    private Dictionary<string, string?> AddCommonParameters(
        IReadOnlyDictionary<string, string?> businessParameters,
        DateTimeOffset startedAt) =>
        new(businessParameters, StringComparer.Ordinal)
        {
            ["app_key"] = _options.AppKey,
            ["timestamp"] = startedAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            ["sign_method"] = "sha256"
        };

    private static Dictionary<string, string?> CreateOrderParameters(
        DateTime startTimePacific,
        DateTime endTimePacific,
        string status,
        string timeType,
        string? fields,
        int pageSize) =>
        new(StringComparer.Ordinal)
        {
            ["start_time"] = startTimePacific.ToString(PacificDateTimeFormat, CultureInfo.InvariantCulture),
            ["end_time"] = endTimePacific.ToString(PacificDateTimeFormat, CultureInfo.InvariantCulture),
            ["status"] = status,
            ["time_type"] = NullIfWhiteSpace(timeType),
            ["fields"] = NullIfWhiteSpace(fields),
            ["page_size"] = Invariant(pageSize)
        };

    private void EnsureConfigured()
    {
        if (!_options.HasAppKey)
        {
            throw new AliExpressConfigurationException("AliExpress:AppKey is not configured.");
        }

        if (!_options.HasAppSecret)
        {
            throw new AliExpressConfigurationException(
                "AliExpress:AppSecret is not configured. Add it to local User Secrets.");
        }
    }

    private static void ValidatePagination(int pageNumber, int pageSize)
    {
        if (pageNumber is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be between 1 and 100.");
        }

        if (pageSize is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 50.");
        }
    }

    private static void ValidateOrderQuery(DateTime startTime, DateTime endTime, string status)
    {
        if (startTime >= endTime)
        {
            throw new ArgumentException("Order query start time must be before end time.");
        }

        if (!AliExpressOrderStatuses.All.Contains(status, StringComparer.Ordinal))
        {
            throw new ArgumentException("Select a documented AliExpress order status.", nameof(status));
        }
    }

    private static string ValidateAliExpressUrl(string sourceUrl)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !(uri.Host.Equals("aliexpress.com", StringComparison.OrdinalIgnoreCase) ||
              uri.Host.EndsWith(".aliexpress.com", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Every source URL must be an absolute AliExpress URL.", nameof(sourceUrl));
        }

        return uri.AbsoluteUri;
    }

    private static string JoinRequiredValues(
        IReadOnlyCollection<string> values,
        int maximumCount,
        string description)
    {
        if (values.Count is < 1 || values.Count > maximumCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(values),
                $"Supply between 1 and {maximumCount} {description}.");
        }

        var joined = JoinOptionalValues(values);
        if (joined is null)
        {
            throw new ArgumentException($"At least one non-empty {description} value is required.", nameof(values));
        }

        return joined;
    }

    private static string? JoinOptionalValues(IReadOnlyCollection<string> values)
    {
        var cleaned = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return cleaned.Length == 0 ? null : string.Join(',', cleaned);
    }

    private static string? FormatPacificTime(DateTime? value) =>
        value?.ToString(PacificDateTimeFormat, CultureInfo.InvariantCulture);

    private static string ToPromotionCountry(string countryCode) =>
        countryCode.Equals("GB", StringComparison.OrdinalIgnoreCase) ? "UK" : countryCode;

    private static IReadOnlyDictionary<string, string> CreateDisplayParameters(
        IReadOnlyDictionary<string, string?> parameters) =>
        parameters
            .Where(parameter => !string.IsNullOrEmpty(parameter.Value))
            .ToDictionary(
                parameter => parameter.Key,
                parameter => parameter.Key == "sign" ? "[redacted]" : parameter.Value!,
                StringComparer.Ordinal);

    private static string FormatJson(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (JsonException)
        {
            return content;
        }
    }

    private static (string? Envelope, string? Code, string? Message, bool? Success) InspectResponse(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var envelopeName = default(string);
            var envelope = root;

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name == "error_response" ||
                        property.Name.EndsWith("_response", StringComparison.Ordinal))
                    {
                        envelopeName = property.Name;
                        envelope = property.Value;
                        break;
                    }
                }
            }

            if (envelopeName == "error_response")
            {
                return (
                    envelopeName,
                    GetScalarValue(envelope, "code"),
                    GetScalarValue(envelope, "msg") ?? GetScalarValue(envelope, "message"),
                    false);
            }

            if (envelope.ValueKind == JsonValueKind.Object &&
                envelope.TryGetProperty("resp_result", out var responseResult))
            {
                var code = GetScalarValue(responseResult, "resp_code");
                return (
                    envelopeName,
                    code,
                    GetScalarValue(responseResult, "resp_msg"),
                    code == "200");
            }

            if (envelope.ValueKind == JsonValueKind.Object)
            {
                var code = GetScalarValue(envelope, "code");
                var success = GetBooleanValue(envelope, "success");
                return (
                    envelopeName,
                    code,
                    GetScalarValue(envelope, "msg") ?? GetScalarValue(envelope, "message"),
                    success);
            }

            return (envelopeName, null, null, null);
        }
        catch (JsonException)
        {
            return (null, null, null, null);
        }
    }

    private static string? GetScalarValue(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? property.ToString()
            : null;
    }

    private static bool? GetBooleanValue(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return property.GetBoolean();
        }

        return bool.TryParse(property.ToString(), out var value) ? value : null;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Invariant(int? value) => value?.ToString(CultureInfo.InvariantCulture);

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);
}
