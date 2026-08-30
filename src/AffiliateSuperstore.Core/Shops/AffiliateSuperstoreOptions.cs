namespace AffiliateSuperstore.Core.Shops;

public sealed class AffiliateSuperstoreOptions
{
    public const string SectionName = "Superstore";

    public string BrandName { get; set; } = "Affiliate Superstore";

    public string FallbackTrackingId { get; set; } = string.Empty;

    public List<ShopDefinition> Shops { get; set; } = [];
}
