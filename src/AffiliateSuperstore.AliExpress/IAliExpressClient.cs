namespace AffiliateSuperstore.AliExpress;

public interface IAliExpressClient
{
    IReadOnlyList<AliExpressApiMethodDescriptor> Methods { get; }

    Task<AliExpressApiCallResult> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<AliExpressApiCallResult> GetFeaturedPromotionsAsync(CancellationToken cancellationToken = default);

    Task<AliExpressApiCallResult> GetFeaturedPromotionProductsAsync(
        AliExpressFeaturedPromotionProductsRequest request,
        CancellationToken cancellationToken = default);

    Task<AliExpressApiCallResult> SearchProductsAsync(
        AliExpressProductSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<AliExpressApiCallResult> GenerateAffiliateLinkAsync(
        string sourceUrl,
        CancellationToken cancellationToken = default);

    Task<AliExpressApiCallResult> GenerateAffiliateLinksAsync(
        AliExpressLinkGenerateRequest request,
        CancellationToken cancellationToken = default);

    Task<AliExpressApiCallResult> GetProductDetailsAsync(
        AliExpressProductDetailRequest request,
        CancellationToken cancellationToken = default);

    Task<AliExpressApiCallResult> GetPromotionInfoAsync(
        AliExpressPromotionInfoRequest request,
        CancellationToken cancellationToken = default);

    Task<AliExpressApiCallResult> QueryHotProductsAsync(
        AliExpressProductSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<AliExpressApiCallResult> DownloadHotProductsAsync(
        AliExpressHotProductDownloadRequest request,
        CancellationToken cancellationToken = default);

    Task<AliExpressApiCallResult> GetProductShippingAsync(
        AliExpressProductShippingRequest request,
        CancellationToken cancellationToken = default);

    Task<AliExpressApiCallResult> GetSkuDetailsAsync(
        AliExpressSkuDetailRequest request,
        CancellationToken cancellationToken = default);

    Task<AliExpressApiCallResult> SmartMatchAsync(
        AliExpressSmartMatchRequest request,
        CancellationToken cancellationToken = default);

    Task<AliExpressApiCallResult> GetOrdersAsync(
        AliExpressOrderGetRequest request,
        CancellationToken cancellationToken = default);

    Task<AliExpressApiCallResult> ListOrdersAsync(
        AliExpressOrderListRequest request,
        CancellationToken cancellationToken = default);

    Task<AliExpressApiCallResult> ListOrdersByIndexAsync(
        AliExpressOrderListByIndexRequest request,
        CancellationToken cancellationToken = default);

    Task<AliExpressApiCallResult> GetMerchantLicenseAsync(
        AliExpressMerchantLicenseRequest request,
        CancellationToken cancellationToken = default);
}
