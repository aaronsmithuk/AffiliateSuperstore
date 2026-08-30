using System.Globalization;
using System.Text.RegularExpressions;

var offers = new[]
{
    new Offer("a", "merchant-one", "HC-40-BR", "40cm Kawaii Highland Cow Plush Toy Soft Stuffed Animal", "en", 12.99m, "toys/plush", "image-highland-40-brown", 1, 40),
    new Offer("b", "merchant-two", "BROWN40", "Soft Highland cattle plushie - brown, 40 cm", "en", 14.49m, "toys/plush", "image-highland-40-brown", 1, 40),
    new Offer("c", "merchant-three", "FR-40", "Vache Highland en peluche marron 40 cm", "fr", 13.75m, "toys/plush", "image-highland-40-brown", 1, 40),
    new Offer("d", "merchant-one", "HC-40-2PK", "2pcs Highland cow plush set 40cm", "en", 22.00m, "toys/plush", "image-highland-40-set", 2, 40),
    new Offer("e", "merchant-one", "HC-20-KEY", "Highland cow plush keyring 20 cm", "en", 5.99m, "toys/plush", "image-highland-20-keyring", 1, 20),
    new Offer("f", "merchant-four", "DRAGON-30", "Green dragon stuffed toy 30cm", "en", 9.50m, "toys/plush", "image-dragon-green", 1, 30)
};

var expected = new Dictionary<(string Left, string Right), Relationship>
{
    [("a", "b")] = Relationship.Identical,
    [("a", "c")] = Relationship.Translation,
    [("a", "d")] = Relationship.Bundle,
    [("a", "e")] = Relationship.Variant,
    [("a", "f")] = Relationship.NotMatch
};

var failed = false;
foreach (var fixture in expected)
{
    var left = offers.Single(offer => offer.Id == fixture.Key.Left);
    var right = offers.Single(offer => offer.Id == fixture.Key.Right);
    var decision = Matcher.Compare(left, right);
    Console.WriteLine($"{left.Id}-{right.Id}: {decision.Relationship} ({decision.Confidence:P1}) - {string.Join("; ", decision.Evidence)}");
    if (decision.Relationship == fixture.Value) continue;

    failed = true;
    Console.Error.WriteLine($"Expected {fixture.Value}, received {decision.Relationship}.");
}

return failed ? 1 : 0;

internal sealed record Offer(
    string Id,
    string Merchant,
    string Sku,
    string Title,
    string Language,
    decimal Price,
    string Category,
    string ImageDigest,
    int PackCount,
    int SizeCentimetres);

internal enum Relationship
{
    Identical,
    Translation,
    Bundle,
    Variant,
    SuspectedDuplicate,
    NotMatch
}

internal sealed record MatchDecision(
    Relationship Relationship,
    decimal Confidence,
    IReadOnlyList<string> Evidence);

internal static partial class Matcher
{
    private static readonly HashSet<string> GenericWords = new(StringComparer.Ordinal)
    {
        "animal", "brown", "cm", "kawaii", "marron", "peluche", "plush", "plushie",
        "soft", "stuffed", "toy"
    };

    public static MatchDecision Compare(Offer left, Offer right)
    {
        var evidence = new List<string>();
        var sameCategory = string.Equals(left.Category, right.Category, StringComparison.OrdinalIgnoreCase);
        var exactImage = string.Equals(left.ImageDigest, right.ImageDigest, StringComparison.Ordinal);
        var titleSimilarity = Jaccard(Tokens(left.Title), Tokens(right.Title));
        var samePack = left.PackCount == right.PackCount;
        var sameSize = left.SizeCentimetres == right.SizeCentimetres;

        if (sameCategory) evidence.Add("same normalized category");
        if (exactImage) evidence.Add("exact source-image digest");
        if (samePack) evidence.Add($"same pack count ({left.PackCount})");
        else evidence.Add($"pack conflict ({left.PackCount} vs {right.PackCount})");
        if (sameSize) evidence.Add($"same size ({left.SizeCentimetres} cm)");
        else evidence.Add($"size conflict ({left.SizeCentimetres} vs {right.SizeCentimetres} cm)");
        evidence.Add($"title-token Jaccard {titleSimilarity.ToString("0.00", CultureInfo.InvariantCulture)}");
        if (left.Price != right.Price) evidence.Add("price difference treated as weak merchant evidence");

        var sameProductFamily = exactImage ||
            (sameCategory && FamilyTokenOverlap(left.Title, right.Title) >= 2);

        if (sameProductFamily && !samePack)
            return new(Relationship.Bundle, 0.99m, evidence);
        if (sameProductFamily && !sameSize)
            return new(Relationship.Variant, 0.97m, evidence);
        if (exactImage && sameCategory && samePack && sameSize)
        {
            var translated = !string.Equals(left.Language, right.Language, StringComparison.OrdinalIgnoreCase);
            evidence.Add(translated ? $"language differs ({left.Language} vs {right.Language})" : "compatible cross-merchant offer");
            return new(translated ? Relationship.Translation : Relationship.Identical, 0.995m, evidence);
        }
        if (sameCategory && samePack && sameSize && titleSimilarity >= 0.65m)
            return new(Relationship.SuspectedDuplicate, 0.90m, evidence);

        return new(Relationship.NotMatch, 0.05m, evidence);
    }

    private static decimal Jaccard(IReadOnlySet<string> left, IReadOnlySet<string> right)
    {
        if (left.Count == 0 && right.Count == 0) return 1;
        var intersection = left.Intersect(right).Count();
        var union = left.Union(right).Count();
        return union == 0 ? 0 : (decimal)intersection / union;
    }

    private static int FamilyTokenOverlap(string left, string right) =>
        Tokens(left).Intersect(Tokens(right)).Count(token => !GenericWords.Contains(token));

    private static IReadOnlySet<string> Tokens(string value) =>
        TokenPattern().Matches(value.Normalize().ToLowerInvariant())
            .Select(match => match.Value)
            .Where(token => token.Length > 1 && token is not "the" and not "and" and not "for")
            .ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
