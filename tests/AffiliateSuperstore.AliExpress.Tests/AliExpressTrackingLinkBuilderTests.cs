namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AliExpressTrackingLinkBuilderTests
{
    [Fact]
    public void Append_AddsShopCampaignPlacementAndOpaqueClickId()
    {
        var result = AliExpressTrackingLinkBuilder.Append(
            "https://s.click.aliexpress.com/e/test?existing=1",
            new AliExpressTrackingParameters(
                Campaign: "plushies/summer sale",
                Creative: "home hero",
                ClickId: "01J6XYZ",
                SubAffiliate: "wonderaisle"));

        var uri = new Uri(result);

        Assert.Contains("existing=1", uri.Query, StringComparison.Ordinal);
        Assert.Contains("cn=plushies%2Fsummer%20sale", uri.Query, StringComparison.Ordinal);
        Assert.Contains("cv=home%20hero", uri.Query, StringComparison.Ordinal);
        Assert.Contains("dp=01J6XYZ", uri.Query, StringComparison.Ordinal);
        Assert.Contains("af=wonderaisle", uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public void Append_RejectsNonAliExpressHosts()
    {
        Assert.Throws<ArgumentException>(() =>
            AliExpressTrackingLinkBuilder.Append(
                "https://example.com/product",
                new AliExpressTrackingParameters(Campaign: "plushies")));
    }
}
