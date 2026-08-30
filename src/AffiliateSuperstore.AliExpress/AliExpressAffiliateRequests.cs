namespace AffiliateSuperstore.AliExpress;

public sealed record AliExpressLinkGenerateRequest
{
    public IReadOnlyCollection<string> SourceUrls { get; init; } = [];

    public string? TrackingId { get; init; }

    public int PromotionLinkType { get; init; }
}

public sealed record AliExpressFeaturedPromotionProductsRequest
{
    public string? PromotionName { get; init; }

    public string? CategoryId { get; init; }

    public string? Fields { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public string? Sort { get; init; }

    public DateTime? PromotionStartTimePacific { get; init; }

    public DateTime? PromotionEndTimePacific { get; init; }
}

public sealed record AliExpressHotProductDownloadRequest
{
    public string CategoryId { get; init; } = string.Empty;

    public string? Fields { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public string LocaleSite { get; init; } = "global";
}

public sealed record AliExpressProductDetailRequest
{
    public IReadOnlyCollection<string> ProductIds { get; init; } = [];

    public string? Fields { get; init; }
}

public sealed record AliExpressPromotionInfoRequest
{
    public IReadOnlyCollection<string> ProductIds { get; init; } = [];
}

public sealed record AliExpressProductShippingRequest
{
    public string ProductId { get; init; } = string.Empty;

    public string SkuId { get; init; } = string.Empty;

    public string TargetSalePrice { get; init; } = string.Empty;

    public string TaxRate { get; init; } = string.Empty;
}

public sealed record AliExpressSkuDetailRequest
{
    public string ProductId { get; init; } = string.Empty;

    public IReadOnlyCollection<string> SkuIds { get; init; } = [];

    public bool IncludeDeliveryInformation { get; init; }
}

public sealed record AliExpressSmartMatchRequest
{
    public string? DeviceId { get; init; }

    public string? ProductId { get; init; }

    public string? Keywords { get; init; }

    public string? Site { get; init; }

    public string? User { get; init; }

    public string? App { get; init; }

    public string? Device { get; init; }

    public string? Fields { get; init; }

    public int PageNumber { get; init; } = 1;
}

public sealed record AliExpressOrderGetRequest
{
    public IReadOnlyCollection<string> OrderIds { get; init; } = [];

    public string? Fields { get; init; }
}

public sealed record AliExpressOrderListRequest
{
    public DateTime StartTimePacific { get; init; } = DateTime.Today.AddDays(-7);

    public DateTime EndTimePacific { get; init; } = DateTime.Today;

    public string Status { get; init; } = AliExpressOrderStatuses.PaymentCompleted;

    public string TimeType { get; init; } = AliExpressOrderTimeTypes.PaymentCompleted;

    public string? Fields { get; init; }

    public string? LocaleSite { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

public sealed record AliExpressOrderListByIndexRequest
{
    public DateTime StartTimePacific { get; init; } = DateTime.Today.AddDays(-7);

    public DateTime EndTimePacific { get; init; } = DateTime.Today;

    public string Status { get; init; } = AliExpressOrderStatuses.PaymentCompleted;

    public string TimeType { get; init; } = AliExpressOrderTimeTypes.PaymentCompleted;

    public string? Fields { get; init; }

    public int PageSize { get; init; } = 20;

    public string? StartQueryIndexId { get; init; }
}

public sealed record AliExpressMerchantLicenseRequest
{
    public string SellerAdminSequence { get; init; } = string.Empty;

    public string? Channel { get; init; }
}

public static class AliExpressOrderStatuses
{
    public const string PaymentCompleted = "Payment Completed";
    public const string BuyerConfirmedReceipt = "Buyer Confirmed Receipt";
    public const string CompletedSettlement = "Completed Settlement";
    public const string Invalid = "Invalid";

    public static IReadOnlyList<string> All { get; } =
    [
        PaymentCompleted,
        BuyerConfirmedReceipt,
        CompletedSettlement,
        Invalid
    ];
}

public static class AliExpressOrderTimeTypes
{
    public const string PaymentCompleted = "Payment Completed Time";
    public const string BuyerConfirmedReceipt = "Buyer Confirmed Receipt Time";
    public const string CompletedSettlement = "Completed Settlement Time";

    public static IReadOnlyList<string> All { get; } =
    [
        PaymentCompleted,
        BuyerConfirmedReceipt,
        CompletedSettlement
    ];
}
