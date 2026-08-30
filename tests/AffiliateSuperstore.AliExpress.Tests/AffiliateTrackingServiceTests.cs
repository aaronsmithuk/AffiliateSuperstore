using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Core.Tracking;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AffiliateTrackingServiceTests
{
    [Fact]
    public void Create_CombinesDurableTrackingIdWithDetailedClickAttribution()
    {
        var service = new AffiliateTrackingService(new FixedClickIdGenerator(), new AffiliateSuperstoreOptions());
        var shop = new ShopDefinition
        {
            Slug = "plushies",
            DisplayName = "The Plushy Shop",
            PathPrefix = "/plushies",
            TrackingId = "theplushyshop",
            SubAffiliateCode = "umbrella"
        };

        var result = service.Create(
            "https://s.click.aliexpress.com/e/example",
            shop,
            "basket",
            "christmas-2026");

        Assert.Equal("theplushyshop", result.TrackingId);
        Assert.Equal("plushies", result.ShopSlug);
        Assert.Equal("fixed-opaque-id", result.ClickId);
        Assert.Contains("cn=christmas-2026", result.Url, StringComparison.Ordinal);
        Assert.Contains("cv=basket", result.Url, StringComparison.Ordinal);
        Assert.Contains("dp=fixed-opaque-id", result.Url, StringComparison.Ordinal);
        Assert.Contains("af=umbrella", result.Url, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_UsesShopSlugAsDefaultCampaign()
    {
        var service = new AffiliateTrackingService(new FixedClickIdGenerator(), new AffiliateSuperstoreOptions());
        var shop = new ShopDefinition
        {
            Slug = "plushies",
            DisplayName = "The Plushy Shop",
            PathPrefix = "/plushies",
            TrackingId = "theplushyshop"
        };

        var result = service.Create("https://s.click.aliexpress.com/e/example", shop, "product-cta");

        Assert.Equal("plushies", result.Campaign);
        Assert.Contains("cn=plushies", result.Url, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_UsesConfiguredFallbackForAShopWithoutDedicatedTrackingId()
    {
        var service = new AffiliateTrackingService(
            new FixedClickIdGenerator(),
            new AffiliateSuperstoreOptions { FallbackTrackingId = "generalstore" });
        var shop = new ShopDefinition
        {
            Slug = "collectables",
            DisplayName = "Collectables",
            PathPrefix = "/collectables"
        };

        var result = service.Create("https://s.click.aliexpress.com/e/example", shop, "search-card");

        Assert.Equal("generalstore", result.TrackingId);
    }

    private sealed class FixedClickIdGenerator : IClickIdGenerator
    {
        public string Create() => "fixed-opaque-id";
    }
}
