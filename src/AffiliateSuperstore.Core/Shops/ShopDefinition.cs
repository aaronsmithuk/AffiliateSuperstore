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

    public List<string> DiscoveryQueries { get; set; } = [];

    public int DiscoveryPagesPerQuery { get; set; } = 1;

    public bool HotProductDiscoveryEnabled { get; set; }

    public bool SmartMatchDiscoveryEnabled { get; set; }

    public int AdvancedDiscoveryPagesPerQuery { get; set; } = 1;

    public string SeoTitle { get; set; } = string.Empty;

    public string SeoDescription { get; set; } = string.Empty;

    public ShopTheme Theme { get; set; } = new();
}

public sealed class ShopTheme
{
    public string Profile { get; set; } = "default";

    public string PrimaryColour { get; set; } = "#134e4a";

    public string AccentColour { get; set; } = "#d8f49a";

    public string CanvasColour { get; set; } = "#f7f8f5";

    public string SurfaceColour { get; set; } = "#ffffff";

    public string TextColour { get; set; } = "#182026";

    public string LogoText { get; set; } = string.Empty;
}
