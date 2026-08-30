namespace AffiliateSuperstore.AliExpress;

public sealed record AliExpressProductSearchRequest
{
    public string Keywords { get; init; } = "plush toy";

    public string? CategoryIds { get; init; }

    public string? Fields { get; init; }

    public int? MinimumSalePriceInCents { get; init; }

    public int? MaximumSalePriceInCents { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public string Sort { get; init; } = "LAST_VOLUME_DESC";

    public string? PlatformProductType { get; init; }

    public string? PromotionName { get; init; }

    public int? DeliveryDays { get; init; }
}
