using AffiliateSuperstore.Application.Catalogue;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class CollectionCandidateMatcherTests
{
    [Fact]
    public void Assess_SuggestsAnExactDistinctiveQueryMatchWithExplanation()
    {
        var result = CollectionCandidateMatcher.Assess(
            "Ocean & River Friends",
            "Seals, otters, whales and other water-loving characters.",
            ["seal plush toy", "axolotl plush toy"],
            "Large kawaii seal stuffed animal",
            null,
            "Stuffed Animals & Plush",
            null);

        Assert.True(result.IsSuggested);
        Assert.True(result.Score >= CollectionCandidateMatcher.SuggestedScore);
        Assert.Equal(["seal plush toy"], result.MatchedQueries);
        Assert.Contains(result.Reasons, reason =>
            reason.Contains("seal plush toy", StringComparison.OrdinalIgnoreCase) &&
            reason.Contains("seal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Assess_DoesNotTreatGenericPlushWordingAsACollectionMatch()
    {
        var result = CollectionCandidateMatcher.Assess(
            "Cute Food & Novelty",
            "Fruit, snacks, drinks and food-animal mash-ups with soft edges.",
            ["cute food plush", "fruit plush toy"],
            "Cute kawaii soft plush toy",
            null,
            "Stuffed Animals & Plush",
            null);

        Assert.False(result.IsSuggested);
        Assert.Equal(0, result.Score);
        Assert.Empty(result.MatchedQueries);
    }

    [Fact]
    public void Assess_NormalizesPluralsAndCommonMiniPlushSynonyms()
    {
        var result = CollectionCandidateMatcher.Assess(
            "Minis & Bag Charms",
            "Small plush keyrings, bag charms and pocket-sized companions.",
            ["mini plush keychain", "plush bag charm"],
            "Tiny fox bag keyring",
            null,
            "Plush Accessories",
            null);

        Assert.True(result.IsSuggested);
        Assert.Equal("mini plush keychain", result.MatchedQueries[0]);
        Assert.Contains(result.Reasons, reason => reason.Contains("keychain", StringComparison.Ordinal));
    }

    [Fact]
    public void Assess_UsesNormalizedIdentityTextButCategoryAloneIsOnlySupportingEvidence()
    {
        var identityMatch = CollectionCandidateMatcher.Assess(
            "Ocean & River Friends",
            "Seals, otters, whales, sharks and axolotls.",
            ["axolotl plush toy"],
            "New arrival",
            null,
            "Stuffed Animals & Plush",
            "pink axolotl plush");
        var categoryOnly = CollectionCandidateMatcher.Assess(
            "Plush Cushions",
            "Pillow-shaped and long-form plushies for beds and sofas.",
            ["plush cushion", "long plush pillow"],
            "New arrival",
            null,
            "Decorative Cushions",
            null);

        Assert.True(identityMatch.IsSuggested);
        Assert.False(categoryOnly.IsSuggested);
        Assert.True(categoryOnly.Score < CollectionCandidateMatcher.SuggestedScore);
        Assert.Contains(categoryOnly.Reasons, reason => reason.Contains("Source category", StringComparison.Ordinal));
    }

    [Fact]
    public void Assess_AllowsCategoryEvidenceToCompleteAPartialTitleMatch()
    {
        var result = CollectionCandidateMatcher.Assess(
            "Animal Friends",
            "Cows, rabbits, pigs, bears and woodland companions.",
            ["woodland animal plush"],
            "Woodland fox cushion",
            null,
            "Stuffed Animals & Plush",
            null);

        Assert.True(result.IsSuggested);
        Assert.Contains(result.Reasons, reason =>
            reason.Contains("animal", StringComparison.Ordinal) &&
            reason.Contains("woodland", StringComparison.Ordinal));
    }
}
