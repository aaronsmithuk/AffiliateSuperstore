namespace AffiliateSuperstore.Core.Shops;

public sealed class AffiliateSuperstoreOptions
{
    public const string SectionName = "Superstore";

    public string BrandName { get; set; } = "Wonder Aisle";

    public string CanonicalBaseUrl { get; set; } = "https://wonderaisle.co.uk";

    public string FallbackTrackingId { get; set; } = string.Empty;

    public List<ShopDefinition> Shops { get; set; } = [];

    public string BuildPublicUrl(string path = "/") =>
        $"{CanonicalBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
}
