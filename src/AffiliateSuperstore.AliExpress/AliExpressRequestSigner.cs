using System.Security.Cryptography;
using System.Text;

namespace AffiliateSuperstore.AliExpress;

public sealed class AliExpressRequestSigner
{
    public string CreateCanonicalString(IEnumerable<KeyValuePair<string, string?>> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var canonical = new StringBuilder();

        foreach (var parameter in parameters
                     .Where(parameter =>
                         !string.Equals(parameter.Key, "sign", StringComparison.Ordinal) &&
                         !string.IsNullOrEmpty(parameter.Key) &&
                         !string.IsNullOrEmpty(parameter.Value))
                     .OrderBy(parameter => parameter.Key, StringComparer.Ordinal))
        {
            canonical.Append(parameter.Key);
            canonical.Append(parameter.Value);
        }

        return canonical.ToString();
    }

    public string Sign(IEnumerable<KeyValuePair<string, string?>> parameters, string appSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appSecret);
        return SignCanonicalString(CreateCanonicalString(parameters), appSecret);
    }

    public string SignSystemRequest(
        string apiPath,
        IEnumerable<KeyValuePair<string, string?>> parameters,
        string appSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiPath);
        return SignCanonicalString(apiPath + CreateCanonicalString(parameters), appSecret);
    }

    public string SignCanonicalString(string canonicalString, string appSecret)
    {
        ArgumentNullException.ThrowIfNull(canonicalString);
        ArgumentException.ThrowIfNullOrWhiteSpace(appSecret);

        var secretBytes = Encoding.UTF8.GetBytes(appSecret);
        var contentBytes = Encoding.UTF8.GetBytes(canonicalString);
        var digest = HMACSHA256.HashData(secretBytes, contentBytes);

        return Convert.ToHexString(digest);
    }
}
