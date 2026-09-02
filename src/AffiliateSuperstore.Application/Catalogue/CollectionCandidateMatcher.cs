using System.Text.RegularExpressions;

namespace AffiliateSuperstore.Application.Catalogue;

public static partial class CollectionCandidateMatcher
{
    public const int SuggestedScore = 65;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "collection", "collections", "companion", "cute", "design",
        "for", "friend", "from", "generic", "inspired", "kawaii", "original", "or", "other",
        "plush", "plushie", "plushy", "soft", "the", "toy", "with"
    };

    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["keyring"] = "keychain",
        ["miniature"] = "mini",
        ["small"] = "mini",
        ["tiny"] = "mini"
    };

    public static CollectionCandidateMatchAssessment Assess(
        string collectionName,
        string collectionDescription,
        IReadOnlyList<string> discoveryQueries,
        string sourceTitle,
        string? editorialTitle,
        string? sourceCategory,
        string? normalizedIdentityTitle)
    {
        var titleTerms = Tokenize(string.Join(' ', new[]
        {
            sourceTitle,
            editorialTitle,
            normalizedIdentityTitle
        }.Where(item => !string.IsNullOrWhiteSpace(item))));
        var categoryTerms = Tokenize(sourceCategory);
        var scopeTerms = Tokenize($"{collectionName} {collectionDescription}");
        var matchedScopeTerms = scopeTerms.Intersect(titleTerms, StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var matchedCategoryTerms = scopeTerms.Intersect(categoryTerms, StringComparer.Ordinal)
            .Except(matchedScopeTerms, StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        var queryMatches = discoveryQueries
            .Where(query => !string.IsNullOrWhiteSpace(query))
            .Select(query => AssessQuery(query.Trim(), titleTerms, categoryTerms))
            .Where(match => match.MatchedTerms.Count > 0)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Query, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var bestQueryScore = queryMatches.FirstOrDefault()?.Score ?? 0;
        var supportingScopeScore = Math.Min(20, matchedScopeTerms.Length * 5);
        var categoryScore = Math.Min(5, matchedCategoryTerms.Length * 2);
        var score = bestQueryScore > 0
            ? Math.Min(100, bestQueryScore + supportingScopeScore + categoryScore)
            : matchedScopeTerms.Length > 0
                ? Math.Min(80, 55 + matchedScopeTerms.Length * 10 + categoryScore)
                : 0;

        var reasons = new List<string>();
        if (queryMatches.Length > 0)
        {
            var best = queryMatches[0];
            reasons.Add($"Discovery query \"{best.Query}\" matched {string.Join(", ", best.MatchedTerms)}.");
        }
        if (matchedScopeTerms.Length > 0)
        {
            reasons.Add($"Collection scope matched {string.Join(", ", matchedScopeTerms)}.");
        }
        if (matchedCategoryTerms.Length > 0)
        {
            reasons.Add($"Source category supports {string.Join(", ", matchedCategoryTerms)}.");
        }
        if (reasons.Count == 0)
        {
            reasons.Add("No distinctive discovery-query or collection-scope terms matched.");
        }

        return new CollectionCandidateMatchAssessment(score, reasons, queryMatches.Select(item => item.Query).ToArray());
    }

    private static QueryMatch AssessQuery(
        string query,
        IReadOnlySet<string> titleTerms,
        IReadOnlySet<string> categoryTerms)
    {
        var queryTerms = Tokenize(query);
        var primaryTerms = queryTerms.Intersect(titleTerms, StringComparer.Ordinal).ToArray();
        if (primaryTerms.Length == 0) return new QueryMatch(query, 0, []);
        var categorySupport = queryTerms.Except(primaryTerms, StringComparer.Ordinal)
            .Intersect(categoryTerms, StringComparer.Ordinal);
        var matchedTerms = primaryTerms.Concat(categorySupport)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        var coverage = (decimal)matchedTerms.Length / queryTerms.Count;
        var score = matchedTerms.Length == queryTerms.Count
            ? Math.Min(90, 70 + queryTerms.Count * 5)
            : (int)Math.Round(coverage * 50m, MidpointRounding.AwayFromZero);
        return new QueryMatch(query, score, matchedTerms);
    }

    private static HashSet<string> Tokenize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        return WordPattern().Matches(value.ToLowerInvariant())
            .Select(match => NormalizeTerm(match.Value))
            .Where(term => term.Length > 1 && !StopWords.Contains(term))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string NormalizeTerm(string term)
    {
        if (term.Length > 4 && term.EndsWith("ies", StringComparison.Ordinal))
        {
            term = term[..^3] + "y";
        }
        else if (term.Length > 3 && term.EndsWith('s') && !term.EndsWith("ss", StringComparison.Ordinal))
        {
            term = term[..^1];
        }
        return Aliases.TryGetValue(term, out var alias) ? alias : term;
    }

    [GeneratedRegex("[\\p{L}\\p{Nd}]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();

    private sealed record QueryMatch(string Query, int Score, IReadOnlyList<string> MatchedTerms);
}

public sealed record CollectionCandidateMatchAssessment(
    int Score,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> MatchedQueries)
{
    public bool IsSuggested => Score >= CollectionCandidateMatcher.SuggestedScore;
}
