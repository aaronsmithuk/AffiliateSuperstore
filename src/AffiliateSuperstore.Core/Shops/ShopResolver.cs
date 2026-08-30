namespace AffiliateSuperstore.Core.Shops;

public interface IShopResolver
{
    ShopDefinition? Resolve(string? host, string? path);
}

public sealed class ShopResolver : IShopResolver
{
    private readonly IReadOnlyList<ShopDefinition> _shops;

    public ShopResolver(AffiliateSuperstoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Validate(options.Shops);

        _shops = options.Shops
            .Where(shop => shop.IsEnabled)
            .OrderByDescending(shop => NormalisePath(shop.PathPrefix).Length)
            .ToArray();
    }

    public ShopDefinition? Resolve(string? host, string? path)
    {
        var normalisedHost = NormaliseHost(host);
        var normalisedPath = NormalisePath(path);

        return _shops.FirstOrDefault(shop =>
            MatchesHost(shop, normalisedHost) && MatchesPath(shop, normalisedPath));
    }

    private static bool MatchesHost(ShopDefinition shop, string host) =>
        shop.Hostnames.Count == 0 ||
        shop.Hostnames.Any(candidate =>
            string.Equals(NormaliseHost(candidate), host, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesPath(ShopDefinition shop, string path)
    {
        var prefix = NormalisePath(shop.PathPrefix);
        return prefix == "/" ||
               string.Equals(path, prefix, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormaliseHost(string? host)
    {
        var value = (host ?? string.Empty).Trim().TrimEnd('.');
        var colon = value.LastIndexOf(':');
        return colon > -1 && value[(colon + 1)..].All(char.IsDigit)
            ? value[..colon].ToLowerInvariant()
            : value.ToLowerInvariant();
    }

    private static string NormalisePath(string? path)
    {
        var value = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
        if (!value.StartsWith('/')) value = "/" + value;
        return value.Length == 1 ? value : value.TrimEnd('/');
    }

    private static void Validate(IReadOnlyCollection<ShopDefinition> shops)
    {
        if (shops.Count == 0)
        {
            throw new InvalidOperationException("At least one shop must be configured.");
        }

        foreach (var shop in shops)
        {
            if (string.IsNullOrWhiteSpace(shop.Slug) || string.IsNullOrWhiteSpace(shop.DisplayName))
            {
                throw new InvalidOperationException("Every shop requires a slug and display name.");
            }

            if (string.IsNullOrWhiteSpace(shop.PathPrefix))
            {
                throw new InvalidOperationException($"Shop '{shop.Slug}' requires a path prefix.");
            }
        }

        var duplicateSlug = shops.GroupBy(shop => shop.Slug, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicateSlug is not null)
        {
            throw new InvalidOperationException($"Shop slug '{duplicateSlug.Key}' is configured more than once.");
        }
    }
}
