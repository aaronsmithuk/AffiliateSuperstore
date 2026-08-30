using System.Text.Json;

namespace AffiliateSuperstore.Application.Basket;

public sealed class AnonymousBasketCodec
{
    private const int MaximumItemsPerShop = 30;

    public IReadOnlyList<string> Get(string? serialized, string shopSlug)
    {
        var state = Read(serialized);
        return state.Shops.TryGetValue(NormaliseShop(shopSlug), out var products) ? products : [];
    }

    public string Add(string? serialized, string shopSlug, string productId)
    {
        var state = Read(serialized);
        var key = NormaliseShop(shopSlug);
        if (!state.Shops.TryGetValue(key, out var products))
        {
            products = [];
            state.Shops[key] = products;
        }

        products.RemoveAll(item => string.Equals(item, productId, StringComparison.Ordinal));
        products.Insert(0, productId);
        if (products.Count > MaximumItemsPerShop)
        {
            products.RemoveRange(MaximumItemsPerShop, products.Count - MaximumItemsPerShop);
        }
        return JsonSerializer.Serialize(state);
    }

    public string? Remove(string? serialized, string shopSlug, string productId)
    {
        var state = Read(serialized);
        var key = NormaliseShop(shopSlug);
        if (state.Shops.TryGetValue(key, out var products))
        {
            products.RemoveAll(item => string.Equals(item, productId, StringComparison.Ordinal));
            if (products.Count == 0) state.Shops.Remove(key);
        }
        return WriteOrNull(state);
    }

    public string? Clear(string? serialized, string shopSlug)
    {
        var state = Read(serialized);
        state.Shops.Remove(NormaliseShop(shopSlug));
        return WriteOrNull(state);
    }

    private static BasketState Read(string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized)) return new BasketState();
        try
        {
            return JsonSerializer.Deserialize<BasketState>(serialized) ?? new BasketState();
        }
        catch (JsonException)
        {
            return new BasketState();
        }
    }

    private static string? WriteOrNull(BasketState state) =>
        state.Shops.Count == 0 ? null : JsonSerializer.Serialize(state);

    private static string NormaliseShop(string shopSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        return shopSlug.Trim().ToLowerInvariant();
    }

    private sealed class BasketState
    {
        public Dictionary<string, List<string>> Shops { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
