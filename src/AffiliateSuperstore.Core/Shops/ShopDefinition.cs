namespace AffiliateSuperstore.Core.Shops;

public sealed class ShopDefinition
{
    public string Slug { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PathPrefix { get; set; } = string.Empty;

    public List<string> Hostnames { get; set; } = [];

    public string TrackingId { get; set; } = string.Empty;

    public string SubAffiliateCode { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public string DefaultSearchQuery { get; set; } = string.Empty;

    public string SeoTitle { get; set; } = string.Empty;

    public string SeoDescription { get; set; } = string.Empty;

    public ShopTheme Theme { get; set; } = new();
}

public sealed class ShopTheme
{
    public string PrimaryColour { get; set; } = "#134e4a";

    public string AccentColour { get; set; } = "#d8f49a";

    public string LogoText { get; set; } = string.Empty;
}
