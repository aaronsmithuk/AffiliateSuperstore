namespace AffiliateSuperstore.AliExpress;

public sealed record AliExpressCategory(
    string CategoryId,
    string Name,
    string? ParentCategoryId);

public sealed record AliExpressProduct(
    string ProductId,
    string? SkuId,
    string Title,
    string? MainImageUrl,
    string? ProductDetailUrl,
    string? PromotionLink,
    string? TargetSalePrice,
    string? TargetOriginalPrice,
    string? Currency,
    string? CommissionRate,
    string? HotProductCommissionRate,
    string? Discount,
    string? EvaluationRate,
    long? RecentSalesVolume,
    string? FirstLevelCategoryId,
    string? FirstLevelCategoryName,
    string? SecondLevelCategoryId,
    string? SecondLevelCategoryName,
    string? ShopId,
    string? ShopName,
    string? ShopUrl,
    string? TaxRate);

public sealed record AliExpressPromotionLink(
    string SourceUrl,
    string PromotionUrl,
    string? Message);

public sealed record AliExpressFeaturedPromotion(
    string Name,
    string? Description,
    int? ProductCount);

public sealed record AliExpressOrder(
    string SubOrderId,
    string? ParentOrderId,
    string? Status,
    string? ProductId,
    string? ProductTitle,
    string? TrackingId,
    string? CustomParameters,
    string? CommissionRate,
    decimal? EstimatedPaidCommission,
    decimal? EstimatedFinishedCommission,
    decimal? PaidAmount,
    decimal? FinishedAmount,
    string? SettledCurrency,
    string? PaidTime,
    string? FinishedTime,
    string? CompletedSettlementTime,
    string? ShipToCountry,
    bool? IsAffiliateProduct,
    bool? IsHotProduct,
    string? RawJson = null,
    string? IncentiveCommissionRate = null,
    decimal? EstimatedIncentivePaidCommission = null,
    decimal? NewBuyerBonusCommission = null,
    bool? IsNewBuyer = null,
    string? OrderPlatform = null,
    string? OrderType = null);

public sealed record AliExpressPage<T>(
    IReadOnlyList<T> Items,
    int? CurrentPage,
    int? TotalPages,
    int? TotalRecords,
    string? MinimumQueryIndexId = null,
    string? MaximumQueryIndexId = null);
