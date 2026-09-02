namespace AffiliateSuperstore.Application.Catalogue;

public static class CatalogueAutonomousTriagePolicy
{
    public const string AutomaticRetirementReasonPrefix = "Automatically retired by catalogue policy";

    private static readonly HashSet<string> RetirementCodes = new(StringComparer.Ordinal)
    {
        "scope.non-plush-product",
        "scope.pet-product",
        "scope.pet-category",
        "scope.missing-plush-evidence",
        "safety.tobacco-themed"
    };

    public static IReadOnlyList<string> RetirementReasons(IEnumerable<ProductQualityFlag> flags) =>
        flags.Select(flag => flag.Code)
            .Where(RetirementCodes.Contains)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    public static bool IsRetirementCode(string code) => RetirementCodes.Contains(code);
}
