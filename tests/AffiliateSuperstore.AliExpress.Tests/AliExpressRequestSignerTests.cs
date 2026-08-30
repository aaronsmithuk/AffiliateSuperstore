using System.Text;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AliExpressRequestSignerTests
{
    private readonly AliExpressRequestSigner _signer = new();

    [Fact]
    public void CreateCanonicalString_SortsUsingOrdinalKeyOrderAndOmitsSign()
    {
        var parameters = new Dictionary<string, string?>
        {
            ["timestamp"] = "1651113997000",
            ["method"] = "aliexpress.affiliate.category.get",
            ["sign"] = "do-not-include",
            ["app_key"] = "12345678",
            ["empty"] = null,
            ["sign_method"] = "sha256"
        };

        var result = _signer.CreateCanonicalString(parameters);

        Assert.Equal(
            "app_key12345678methodaliexpress.affiliate.category.getsign_methodsha256timestamp1651113997000",
            result);
    }

    [Fact]
    public void SignCanonicalString_UsesHmacSha256AndUppercaseHex()
    {
        const string content = "The quick brown fox jumps over the lazy dog";

        var result = _signer.SignCanonicalString(content, "key");

        Assert.Equal(
            "F7BC83F430538424B13298E6AA6FB143EF4D59A14946175997479DBC2D1A3CD8",
            result);
    }

    [Fact]
    public void SignSystemRequest_PrefixesApiPathBeforeCanonicalParameters()
    {
        var parameters = new Dictionary<string, string?>
        {
            ["timestamp"] = "1700000000000",
            ["app_key"] = "12345678",
            ["sign_method"] = "sha256"
        };

        var result = _signer.SignSystemRequest("/aliexpress/xinghe/merchant/license/get", parameters, "secret");
        var expected = _signer.SignCanonicalString(
            "/aliexpress/xinghe/merchant/license/getapp_key12345678sign_methodsha256timestamp1700000000000",
            "secret");

        Assert.Equal(expected, result);
    }
}
