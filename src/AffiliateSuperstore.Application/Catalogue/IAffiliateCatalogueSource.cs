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

    public async Task<IReadOnlyList<AliExpressPromotionLink>> GenerateLinksAsync(
        IReadOnlyCollection<string> sourceUrls,
        string trackingId,
        CancellationToken cancellationToken = default)
    {
        if (sourceUrls.Count == 0) return [];

        var result = await client.GenerateAffiliateLinksAsync(
            new AliExpressLinkGenerateRequest
            {
                SourceUrls = sourceUrls,
                TrackingId = trackingId,
                PromotionLinkType = 0
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
