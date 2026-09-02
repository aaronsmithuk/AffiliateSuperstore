using AffiliateSuperstore.AliExpress;

namespace AffiliateSuperstore.Application.Catalogue;

public interface IAffiliateCatalogueSource
{
    Task<AliExpressPage<AliExpressProduct>> SearchAsync(
        string keywords,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AliExpressPromotionLink>> GenerateLinksAsync(
        IReadOnlyCollection<string> sourceUrls,
        string trackingId,
        CancellationToken cancellationToken = default);

    Task<AliExpressPage<AliExpressProduct>> SearchHotProductsAsync(
        string keywords,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This catalogue source does not support hot-product discovery.");

    Task<AliExpressPage<AliExpressProduct>> SmartMatchAsync(
        string? productId,
        string? keywords,
        int pageNumber,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This catalogue source does not support smart-match discovery.");

    Task<IReadOnlyList<AliExpressPromotionLink>> GenerateLinksAsync(
        IReadOnlyCollection<string> sourceUrls,
        string trackingId,
        int promotionLinkType,
        CancellationToken cancellationToken = default) => promotionLinkType == 0
            ? GenerateLinksAsync(sourceUrls, trackingId, cancellationToken)
            : throw new NotSupportedException("This catalogue source does not support hot-product links.");
}

public interface IAffiliateProductDetailSource
{
    Task<IReadOnlyList<AliExpressProduct>> GetDetailsAsync(
        IReadOnlyCollection<string> productIds,
        CancellationToken cancellationToken = default);
}

public sealed class AliExpressCatalogueSource(IAliExpressClient client) : IAffiliateCatalogueSource
{
    public async Task<AliExpressPage<AliExpressProduct>> SearchAsync(
        string keywords,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await client.SearchProductsAsync(
            new AliExpressProductSearchRequest
            {
                Keywords = keywords,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Sort = "LAST_VOLUME_DESC"
            },
            cancellationToken);

        EnsureSuccess(result, "product search");
        return AliExpressResponseReader.ReadProducts(result.RawResponse);
    }

    public async Task<AliExpressPage<AliExpressProduct>> SearchHotProductsAsync(
        string keywords,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await client.QueryHotProductsAsync(
            new AliExpressProductSearchRequest
            {
                Keywords = keywords,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Sort = "LAST_VOLUME_DESC"
            },
            cancellationToken);

        EnsureSuccess(result, "hot-product search");
        return AliExpressResponseReader.ReadProducts(result.RawResponse);
    }

    public async Task<AliExpressPage<AliExpressProduct>> SmartMatchAsync(
        string? productId,
        string? keywords,
        int pageNumber,
        CancellationToken cancellationToken = default)
    {
        var result = await client.SmartMatchAsync(
            new AliExpressSmartMatchRequest
            {
                ProductId = productId,
                Keywords = keywords,
                PageNumber = pageNumber
            },
            cancellationToken);

        EnsureSuccess(result, "smart-match discovery");
        return AliExpressResponseReader.ReadProducts(result.RawResponse);
    }

    public async Task<IReadOnlyList<AliExpressPromotionLink>> GenerateLinksAsync(
        IReadOnlyCollection<string> sourceUrls,
        string trackingId,
        CancellationToken cancellationToken = default)
        => await GenerateLinksAsync(sourceUrls, trackingId, 0, cancellationToken);

    public async Task<IReadOnlyList<AliExpressPromotionLink>> GenerateLinksAsync(
        IReadOnlyCollection<string> sourceUrls,
        string trackingId,
        int promotionLinkType,
        CancellationToken cancellationToken = default)
    {
        if (sourceUrls.Count == 0) return [];

        var result = await client.GenerateAffiliateLinksAsync(
            new AliExpressLinkGenerateRequest
            {
                SourceUrls = sourceUrls,
                TrackingId = trackingId,
                PromotionLinkType = promotionLinkType
            },
            cancellationToken);

        EnsureSuccess(result, "affiliate-link generation");
        return AliExpressResponseReader.ReadPromotionLinks(result.RawResponse);
    }

    private static void EnsureSuccess(AliExpressApiCallResult result, string operation)
    {
        if (result.IsSuccess) return;

        throw new InvalidOperationException(
            $"AliExpress {operation} failed: {result.PlatformResponseCode ?? ((int)result.HttpStatusCode).ToString()} " +
            $"{result.PlatformResponseMessage}".TrimEnd());
    }
}

public sealed class AliExpressProductDetailSource(IAliExpressClient client) : IAffiliateProductDetailSource
{
    public async Task<IReadOnlyList<AliExpressProduct>> GetDetailsAsync(
        IReadOnlyCollection<string> productIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productIds);
        if (productIds.Count is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(productIds), "AliExpress product detail requests must contain between 1 and 50 product IDs.");
        }

        var result = await client.GetProductDetailsAsync(
            new AliExpressProductDetailRequest { ProductIds = productIds },
            cancellationToken);
        EnsureSuccess(result);
        return AliExpressResponseReader.ReadProducts(result.RawResponse).Items;
    }

    private static void EnsureSuccess(AliExpressApiCallResult result)
    {
        if (result.IsSuccess) return;

        throw new InvalidOperationException(
            $"AliExpress product detail refresh failed: {result.PlatformResponseCode ?? ((int)result.HttpStatusCode).ToString()} " +
            $"{result.PlatformResponseMessage}".TrimEnd());
    }
}
