using AffiliateSuperstore.Application.Basket;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AnonymousBasketCodecTests
{
    private readonly AnonymousBasketCodec _codec = new();

    [Fact]
    public void Add_RoundTripsAndDeduplicatesNewestItem()
    {
        var state = _codec.Add(null, "Plushies", "1001");
        state = _codec.Add(state, "plushies", "1002");
        state = _codec.Add(state, "plushies", "1001");

        Assert.Equal(["1001", "1002"], _codec.Get(state, "PLUSHIES"));
    }

    [Fact]
    public void Add_CapsEachShopAtThirtyItems()
    {
        string? state = null;
        for (var index = 1; index <= 35; index++) state = _codec.Add(state, "plushies", index.ToString());

        var items = _codec.Get(state, "plushies");
        Assert.Equal(30, items.Count);
        Assert.Equal("35", items[0]);
        Assert.DoesNotContain("1", items);
    }

    [Fact]
    public void RemoveAndClear_AreScopedToShop()
    {
        var state = _codec.Add(null, "plushies", "1001");
        state = _codec.Add(state, "collectables", "2001");
        state = _codec.Remove(state, "plushies", "1001");

        Assert.Empty(_codec.Get(state, "plushies"));
        Assert.Equal(["2001"], _codec.Get(state, "collectables"));
        Assert.Null(_codec.Clear(state, "collectables"));
    }

    [Fact]
    public void Get_WithMalformedState_ReturnsEmptyList()
    {
        Assert.Empty(_codec.Get("not-json", "plushies"));
    }
}
