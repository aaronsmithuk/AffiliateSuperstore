using System.Text.RegularExpressions;

namespace AffiliateSuperstore.Web.Services;

public sealed partial class WebAnalyticsOptions
{
    public const string SectionName = "WebAnalytics";

    public bool Enabled { get; set; }

    public string GoogleMeasurementId { get; set; } = string.Empty;

    public bool IsConfigured =>
        Enabled && GoogleMeasurementIdRegex().IsMatch(GoogleMeasurementId.Trim());

    public string NormalizedGoogleMeasurementId => GoogleMeasurementId.Trim().ToUpperInvariant();

    [GeneratedRegex("^G-[A-Z0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex GoogleMeasurementIdRegex();
}
