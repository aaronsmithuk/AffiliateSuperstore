using System.Net;

namespace AffiliateSuperstore.AliExpress;

public sealed record AliExpressApiCallResult(
    string Method,
    DateTimeOffset StartedAtUtc,
    TimeSpan Duration,
    HttpStatusCode HttpStatusCode,
    IReadOnlyDictionary<string, string> RequestParameters,
    string RawResponse,
    string FormattedResponse,
    string? ResponseEnvelope,
    string? PlatformResponseCode,
    string? PlatformResponseMessage,
    bool? PlatformSucceeded)
{
    public bool IsHttpSuccessStatusCode => (int)HttpStatusCode is >= 200 and <= 299;

    public bool IsSuccess =>
        IsHttpSuccessStatusCode &&
        (PlatformSucceeded ?? (PlatformResponseCode is null || PlatformResponseCode == "200")) &&
        !string.Equals(ResponseEnvelope, "error_response", StringComparison.Ordinal);
}
