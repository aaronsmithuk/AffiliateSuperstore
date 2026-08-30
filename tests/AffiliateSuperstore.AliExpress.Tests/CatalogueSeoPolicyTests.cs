using AffiliateSuperstore.Application.Catalogue;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class CatalogueSeoPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 20, 0, 0, TimeSpan.Zero);
    private readonly CatalogueSeoPolicy policy = new(new FixedTimeProvider(Now));

    [Fact]
    public void IsProductIndexable_RequiresOriginalCopyImagePriceAndFreshSnapshot()
    {
        Assert.True(policy.IsProductIndexable(
            "45cm Highland Cow Plush",
            "A soft, huggable 45cm Highland cow plush. Check delivery on AliExpress.",
            "https://example.test/cow.jpg",
            8.89m,
            Now.AddDays(-1)));

        Assert.False(policy.IsProductIndexable(null, new string('a', 70), "image", 8.89m, Now));
        Assert.False(policy.IsProductIndexable("Useful title", "Thin", "image", 8.89m, Now));
        Assert.False(policy.IsProductIndexable("Useful title", new string('a', 70), null, 8.89m, Now));
        Assert.False(policy.IsProductIndexable("Useful title", new string('a', 70), "image", 0, Now));
        Assert.False(policy.IsProductIndexable("Useful title", new string('a', 70), "image", 8.89m, Now.AddDays(-15)));
    }

    [Theory]
    [InlineData(11, false)]
    [InlineData(12, true)]
    public void IsShopIndexable_RequiresEnoughQualityProducts(int count, bool expected) =>
        Assert.Equal(expected, CatalogueSeoPolicy.IsShopIndexable(count));

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
