namespace AffiliateSuperstore.AliExpress;

public sealed class AliExpressOptions
{
    public const string SectionName = "AliExpress";

    public string AppKey { get; set; } = string.Empty;

    public string AppSecret { get; set; } = string.Empty;

    public string TrackingId { get; set; } = string.Empty;

    public Uri Gateway { get; set; } = new("https://api-sg.aliexpress.com/sync");

    public Uri SystemGateway { get; set; } = new("https://api-sg.aliexpress.com/rest");

    public string ShipToCountry { get; set; } = "GB";

    public string TargetCurrency { get; set; } = "GBP";

    public string TargetLanguage { get; set; } = "EN";

    public bool AdvancedApiEnabled { get; set; }

    public bool PromotionInfoApiEnabled { get; set; }

    public bool SkuDimensionApiEnabled { get; set; }

    public bool SystemToolEnabled { get; set; } = true;

    public bool HasAppKey => !string.IsNullOrWhiteSpace(AppKey);

    public bool HasAppSecret => !string.IsNullOrWhiteSpace(AppSecret);

    public bool HasTrackingId => !string.IsNullOrWhiteSpace(TrackingId);

    public bool IsConfigured => HasAppKey && HasAppSecret;
}
