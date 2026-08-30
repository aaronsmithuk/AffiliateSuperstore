using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Core.Shops;

namespace AffiliateSuperstore.Core.Tracking;

public sealed record TrackedOutboundLink(
    string Url,
    string ClickId,
    string ShopSlug,
    string Placement,
    string Campaign,
    string TrackingId);

public interface IClickIdGenerator
{
    string Create();
}

public sealed class GuidClickIdGenerator : IClickIdGenerator
{
    public string Create() => Guid.CreateVersion7().ToString("N");
}

public sealed class AffiliateTrackingService(
    IClickIdGenerator clickIdGenerator,
    AffiliateSuperstoreOptions options)
{
    public TrackedOutboundLink Create(
        string promotionUrl,
        ShopDefinition shop,
        string placement,
        string? campaign = null)
    {
        ArgumentNullException.ThrowIfNull(shop);
        ArgumentException.ThrowIfNullOrWhiteSpace(placement);

        var trackingId = string.IsNullOrWhiteSpace(shop.TrackingId)
            ? options.FallbackTrackingId
            : shop.TrackingId;

        if (string.IsNullOrWhiteSpace(trackingId))
        {
            throw new InvalidOperationException($"Shop '{shop.Slug}' does not have a Tracking ID or fallback assigned.");
        }

        var clickId = clickIdGenerator.Create();
        var campaignName = string.IsNullOrWhiteSpace(campaign) ? shop.Slug : campaign.Trim();
        var url = AliExpressTrackingLinkBuilder.Append(
            promotionUrl,
            new AliExpressTrackingParameters(
                Campaign: campaignName,
                Creative: placement.Trim(),
                ClickId: clickId,
                SubAffiliate: string.IsNullOrWhiteSpace(shop.SubAffiliateCode) ? null : shop.SubAffiliateCode));

        return new TrackedOutboundLink(
            url,
            clickId,
            shop.Slug,
            placement.Trim(),
            campaignName,
            trackingId);
    }
}
