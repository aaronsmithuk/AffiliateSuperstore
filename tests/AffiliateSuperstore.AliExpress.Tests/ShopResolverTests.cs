using AffiliateSuperstore.Core.Shops;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class ShopResolverTests
{
    [Fact]
    public void Resolve_MatchesPathAndIgnoresQueryLikeSuffixes()
    {
        var shop = CreateShop("plushies", "/plushies");
        var resolver = new ShopResolver(new AffiliateSuperstoreOptions { Shops = [shop] });

        Assert.Same(shop, resolver.Resolve("wonderaisle.co.uk", "/plushies"));
        Assert.Same(shop, resolver.Resolve("wonderaisle.co.uk:443", "/plushies/search"));
        Assert.Null(resolver.Resolve("wonderaisle.co.uk", "/collectables"));
    }

    [Fact]
    public void Resolve_UsesLongestMatchingPathPrefix()
    {
        var broad = CreateShop("plushies", "/plushies");
        var narrow = CreateShop("dragons", "/plushies/dragons");
        var resolver = new ShopResolver(new AffiliateSuperstoreOptions { Shops = [broad, narrow] });

        Assert.Same(narrow, resolver.Resolve(null, "/plushies/dragons/green"));
    }

    [Fact]
    public void Resolve_HonoursOptionalHostnameRestriction()
    {
        var shop = CreateShop("plushies", "/plushies");
        shop.Hostnames = ["shop.example.co.uk"];
        var resolver = new ShopResolver(new AffiliateSuperstoreOptions { Shops = [shop] });

        Assert.Same(shop, resolver.Resolve("SHOP.EXAMPLE.CO.UK", "/plushies"));
        Assert.Null(resolver.Resolve("other.example.co.uk", "/plushies"));
    }

    [Fact]
    public void Constructor_RejectsDuplicateShopSlugs()
    {
        var options = new AffiliateSuperstoreOptions
        {
            Shops = [CreateShop("plushies", "/plushies"), CreateShop("PLUSHIES", "/soft-toys")]
        };

        Assert.Throws<InvalidOperationException>(() => new ShopResolver(options));
    }

    [Theory]
    [InlineData("purple", "playful")]
    [InlineData("#6f4bc3", "playful; background:red")]
    public void Constructor_RejectsUnsafeThemeConfiguration(string primaryColour, string profile)
    {
        var shop = CreateShop("plushies", "/plushies");
        shop.Theme.PrimaryColour = primaryColour;
        shop.Theme.Profile = profile;

        Assert.Throws<InvalidOperationException>(() =>
            new ShopResolver(new AffiliateSuperstoreOptions { Shops = [shop] }));
    }

    private static ShopDefinition CreateShop(string slug, string path) => new()
    {
        Slug = slug,
        DisplayName = slug,
        PathPrefix = path,
        TrackingId = "theplushyshop",
        DefaultSearchQuery = "plush toy"
    };
}
