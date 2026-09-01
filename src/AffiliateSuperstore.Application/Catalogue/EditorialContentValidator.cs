using System.Text.Json;
using System.Text.RegularExpressions;
using AffiliateSuperstore.Persistence.Entities;

namespace AffiliateSuperstore.Application.Catalogue;

public enum EditorialFindingSeverity
{
    Warning,
    Blocker
}

public sealed record EditorialValidationFinding(
    string Code,
    EditorialFindingSeverity Severity,
    string Field,
    string Message,
    string? Evidence = null);

public sealed record EditorialValidationInput(
    string SourceTitle,
    string? EditorialTitle,
    string? EditorialDescription,
    string? KnownMaterial = null);

public sealed record EditorialValidationResult(
    EditorialValidationState State,
    IReadOnlyList<EditorialValidationFinding> Findings)
{
    public bool IsBlocked => State == EditorialValidationState.Blocked;
    public string SerializedFindings => JsonSerializer.Serialize(Findings);
}

public sealed partial class EditorialContentValidator
{
    public const string Version = "1.1";
    private static readonly string[] Materials = ["cotton", "polyester", "wool", "velvet", "silk", "linen", "acrylic", "nylon"];

    public EditorialValidationResult Validate(EditorialValidationInput input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SourceTitle);
        var findings = new List<EditorialValidationFinding>();
        var title = input.EditorialTitle?.Trim() ?? string.Empty;
        var description = input.EditorialDescription?.Trim() ?? string.Empty;
        var copy = $"{title} {description}".Trim();

        if (description.Length == 0)
        {
            findings.Add(Warning("copy.description-missing", "description", "Add an original description before approving this product."));
        }
        else if (description.Length < 80)
        {
            findings.Add(Warning("copy.description-thin", "description", "The description is too short to give shoppers useful original context.", $"{description.Length} characters"));
        }
        if (description.Length > 0 && string.Equals(description, input.SourceTitle, StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Warning("copy.source-duplicate", "description", "The description repeats the merchant title instead of adding original value."));
        }

        AddPatternFinding(findings, UnsupportedAuthenticityPattern(), copy, "claim.authenticity", "copy", "Official, licensed, authentic or genuine claims require independent evidence and cannot be added editorially.");
        AddPatternFinding(findings, DeliveryClaimPattern(), copy, "claim.delivery", "copy", "Delivery speed and arrival claims are dynamic and must not be added to editorial copy.");
        AddPatternFinding(findings, PriceClaimPattern(), copy, "claim.price", "copy", "Prices, discounts and cheapest/best-price claims must come from the current source projection, not editorial copy.");
        AddPatternFinding(findings, SafetyClaimPattern(), copy, "claim.safety", "copy", "Safety, age-suitability, medical or care claims require verified product evidence.");
        AddPatternFinding(findings, PerformanceClaimPattern(), copy, "claim.performance", "copy", "Sales, review, rating and popularity claims are time-sensitive and unsupported in editorial copy.");
        AddPatternFinding(findings, SuperlativePattern(), copy, "claim.superlative", "copy", "Absolute quality or ranking claims need objective evidence.");
        AddPatternFinding(findings, PromotionalLanguagePattern(), copy, "copy.promotional-language", "copy", "Promotional or subjective merchant wording must be removed from editorial copy.");
        AddPatternFinding(findings, SourceNarrationPattern(), copy, "copy.source-narration", "copy", "Write consumer-facing copy directly instead of narrating the merchant source.");

        var sourceNumbers = NumericClaimPattern().Matches(input.SourceTitle)
            .Select(match => NormaliseClaim(match.Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in NumericClaimPattern().Matches(copy))
        {
            var claim = NormaliseClaim(match.Value);
            if (claim.Length == 0 || sourceNumbers.Contains(claim)) continue;
            findings.Add(Blocker(
                "claim.unsupported-number",
                FieldForMatch(title, match.Index),
                "A number or measurement was added that is not present in the source title.",
                match.Value));
        }

        var knownMaterial = input.KnownMaterial?.Trim();
        foreach (var material in Materials.Where(material => Word(material).IsMatch(copy)))
        {
            if (Word(material).IsMatch(input.SourceTitle) || string.Equals(material, knownMaterial, StringComparison.OrdinalIgnoreCase)) continue;
            findings.Add(Blocker(
                "claim.unsupported-material",
                Word(material).IsMatch(title) ? "title" : "description",
                "A material claim was added without matching source evidence.",
                material));
        }

        var distinct = findings.DistinctBy(item => (item.Code, item.Field, item.Evidence), EqualityComparer<(string, string, string?)>.Default).ToArray();
        var state = distinct.Any(item => item.Severity == EditorialFindingSeverity.Blocker)
            ? EditorialValidationState.Blocked
            : distinct.Length > 0 ? EditorialValidationState.Warning : EditorialValidationState.Passed;
        return new EditorialValidationResult(state, distinct);
    }

    public static IReadOnlyList<EditorialValidationFinding> ReadFindings(string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized)) return [];
        try { return JsonSerializer.Deserialize<EditorialValidationFinding[]>(serialized) ?? []; }
        catch (JsonException) { return [Warning("validation.invalid-data", "copy", "Stored editorial validation evidence could not be read.")]; }
    }

    private static void AddPatternFinding(
        ICollection<EditorialValidationFinding> findings,
        Regex pattern,
        string copy,
        string code,
        string field,
        string message)
    {
        var match = pattern.Match(copy);
        if (match.Success) findings.Add(Blocker(code, field, message, match.Value));
    }

    private static string FieldForMatch(string title, int copyIndex) => copyIndex < title.Length ? "title" : "description";
    private static string NormaliseClaim(string value) => WhitespacePattern().Replace(value, string.Empty).ToLowerInvariant();
    private static Regex Word(string value) => new($@"\b{Regex.Escape(value)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static EditorialValidationFinding Warning(string code, string field, string message, string? evidence = null) => new(code, EditorialFindingSeverity.Warning, field, message, evidence);
    private static EditorialValidationFinding Blocker(string code, string field, string message, string? evidence = null) => new(code, EditorialFindingSeverity.Blocker, field, message, evidence);

    [GeneratedRegex(@"(?<![\p{L}\p{N}])\d+(?:\.\d+)?\s*(?:mm|cm|metres?|meters?|m|inches?|inch|in|kg|grams?|g|pcs?|pieces?|pack|%)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NumericClaimPattern();

    [GeneratedRegex(@"\b(official|officially\s+licensed|licensed|authentic|genuine|certified)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnsupportedAuthenticityPattern();

    [GeneratedRegex(@"\b(next[- ]day|same[- ]day|fast\s+delivery|free\s+delivery|arrives?\s+(?:in|by)|ships?\s+(?:in|within)|delivery\s+(?:in|within))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DeliveryClaimPattern();

    [GeneratedRegex(@"(?:£|\$|€)\s*\d|\b(?:gbp|usd|eur|cheapest|best\s+price|price\s+guarantee|\d+%\s+off)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PriceClaimPattern();

    [GeneratedRegex(@"\b(non[- ]toxic|hypoallergenic|fire[- ]resistant|flame[- ]retardant|child[- ]safe|baby[- ]safe|safe\s+for|suitable\s+for\s+ages?|machine[- ]washable|medical|therapeutic)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SafetyClaimPattern();

    [GeneratedRegex(@"\b(best[- ]seller|bestselling|most\s+popular|top[- ]rated|five[- ]star|5[- ]star|\d+(?:\.\d+)?\s*stars?|\d+\s*(?:reviews?|sold|orders?))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PerformanceClaimPattern();

    [GeneratedRegex(@"\b(best|finest|perfect|guaranteed|ultimate|premium\s+quality|number\s+one|#1)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SuperlativePattern();

    [GeneratedRegex(@"\b(adorable|amazing|premium|luxurious|must[- ]have|soothing\s+companion)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PromotionalLanguagePattern();

    [GeneratedRegex(@"\b(?:(?:the\s+)?(?:source\s+)?(?:title|listing|seller)\s+(?:says?|describes?|calls?)|described\s+as)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourceNarrationPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}
