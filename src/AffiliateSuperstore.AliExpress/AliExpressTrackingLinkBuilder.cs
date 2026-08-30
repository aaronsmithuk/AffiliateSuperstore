namespace AffiliateSuperstore.AliExpress;

public sealed record AliExpressTrackingParameters(
    string? Campaign = null,
    string? Creative = null,
    string? ClickId = null,
    string? SubAffiliate = null);

public static class AliExpressTrackingLinkBuilder
{
    public static string Append(string promotionUrl, AliExpressTrackingParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (!Uri.TryCreate(promotionUrl, UriKind.Absolute, out var uri) ||
            !(uri.Host.Equals("aliexpress.com", StringComparison.OrdinalIgnoreCase) ||
              uri.Host.EndsWith(".aliexpress.com", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("A valid AliExpress promotion URL is required.", nameof(promotionUrl));
        }

        var additions = new Dictionary<string, string?>
        {
            ["af"] = parameters.SubAffiliate,
            ["cn"] = parameters.Campaign,
            ["cv"] = parameters.Creative,
            ["dp"] = parameters.ClickId
        };

        var queryParts = new List<string>();
        if (uri.Query.Length > 1)
        {
            queryParts.Add(uri.Query[1..]);
        }

        queryParts.AddRange(additions
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{item.Key}={Uri.EscapeDataString(item.Value!.Trim())}"));

        var builder = new UriBuilder(uri)
        {
            Query = string.Join('&', queryParts)
        };

        return builder.Uri.AbsoluteUri;
    }
}
