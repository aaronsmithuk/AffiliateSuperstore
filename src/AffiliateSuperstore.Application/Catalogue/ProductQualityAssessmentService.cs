using System.Text.Json;
using System.Text.RegularExpressions;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record ProductQualityFlag(string Code, string Message);

public sealed record ProductQualityAssessment(IReadOnlyList<ProductQualityFlag> Flags)
{
    public bool RequiresReview => Flags.Count > 0;
    public string SerializedFlags => JsonSerializer.Serialize(Flags);
}

public sealed record ProductReassessmentResult(int ProductsChecked, int ProductsFlagged, int ProductsDemoted);

public sealed partial class ProductQualityAssessmentService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    TimeProvider timeProvider)
{
    private static readonly (Regex Pattern, ProductQualityFlag Flag)[] TitleRules =
    [
        (PetProductPattern(), new("scope.pet-product", "Likely pet toy rather than a collectable plush.")),
        (BabyProductPattern(), new("scope.baby-product", "Baby, cot or stroller product needs additional safety and scope review.")),
        (ThirdPartyCharacterPattern(), new("ip.third-party-character", "Title references a character, celebrity or entertainment property; verify licensing before publication.")),
        (TobaccoPattern(), new("safety.tobacco-themed", "Tobacco-themed product is unsuitable for the initial shop.")),
        (AmbiguousQuantityPattern(), new("listing.ambiguous-quantity", "Listing advertises multiple possible quantities; check that the displayed price is not misleading."))
    ];

    public ProductQualityAssessment Assess(
        string title,
        string? firstLevelCategoryName = null,
        string? secondLevelCategoryName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var flags = TitleRules
            .Where(rule => rule.Pattern.IsMatch(title))
            .Select(rule => rule.Flag)
            .ToList();
        var categories = $"{firstLevelCategoryName} {secondLevelCategoryName}";
        if (PetCategoryPattern().IsMatch(categories) && flags.All(flag => flag.Code != "scope.pet-product"))
        {
            flags.Add(new ProductQualityFlag("scope.pet-category", "AliExpress categorises this as a pet product."));
        }
        if (title.Length > 220)
        {
            flags.Add(new ProductQualityFlag("listing.excessive-title", "Listing title is unusually long and should be edited before publication."));
        }
        return new ProductQualityAssessment(flags.DistinctBy(flag => flag.Code).ToArray());
    }

    public ProductQualityAssessment AssessForPublication(
        string sourceTitle,
        string? editorialTitle,
        string? firstLevelCategoryName = null,
        string? secondLevelCategoryName = null)
    {
        var sourceAssessment = Assess(sourceTitle, firstLevelCategoryName, secondLevelCategoryName);
        if (string.IsNullOrWhiteSpace(editorialTitle))
        {
            return sourceAssessment;
        }

        var editorialAssessment = Assess(editorialTitle);
        return new ProductQualityAssessment(sourceAssessment.Flags
            .Concat(editorialAssessment.Flags)
            .DistinctBy(flag => flag.Code)
            .ToArray());
    }

    public async Task<ProductReassessmentResult> ReassessShopAsync(
        string shopSlug,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var products = await context.ShopProducts
            .Include(item => item.Shop)
            .Include(item => item.Product)
            .Where(item => item.Shop.Slug == shopSlug && item.IsActive)
            .ToListAsync(cancellationToken);
        var flagged = 0;
        var demoted = 0;
        var now = timeProvider.GetUtcNow();
        foreach (var item in products)
        {
            var assessment = AssessForPublication(
                item.Product.Title,
                item.EditorialTitle,
                item.Product.FirstLevelCategoryName,
                item.Product.SecondLevelCategoryName);
            item.AutomatedReviewFlags = assessment.SerializedFlags;
            item.AutomatedReviewedUtc = now;
            if (!assessment.RequiresReview || item.ReviewStatus == ProductReviewStatus.Rejected)
            {
                continue;
            }

            flagged++;
            if (item.ReviewStatus is ProductReviewStatus.Pending or ProductReviewStatus.Approved)
            {
                if (item.ReviewStatus == ProductReviewStatus.Approved) demoted++;
                item.ReviewStatus = ProductReviewStatus.NeedsReview;
            }
        }
        await context.SaveChangesAsync(cancellationToken);
        return new ProductReassessmentResult(products.Count, flagged, demoted);
    }

    public static IReadOnlyList<ProductQualityFlag> ReadFlags(string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized)) return [];
        try { return JsonSerializer.Deserialize<ProductQualityFlag[]>(serialized) ?? []; }
        catch (JsonException) { return [new ProductQualityFlag("review.invalid-flags", "Stored quality flags could not be read.")]; }
    }

    [GeneratedRegex(@"\b(catnip|dog\s+toy|cat\s+toy|pet\s+toy|for\s+cats|for\s+dogs|teeth\s+grinding|pet\s+interactive)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PetProductPattern();

    [GeneratedRegex(@"\b(newborn|baby\s+rattle|stroller|crib|bassinet|teether|cot\s+mobile)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BabyProductPattern();

    [GeneratedRegex(@"\b(mimikyu|eevee|pokemon|pokémon|fnaf|five\s+nights?\s+at\s+freddy|freddy|michael\s+jackson|domo\s+kun|hello\s+kitty|sanrio|disney|marvel|anime\s+(character|peripheral)|game\s+(character|doll)|cosplay)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ThirdPartyCharacterPattern();

    [GeneratedRegex(@"\b(cigar|cigarette|tobacco)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TobaccoPattern();

    [GeneratedRegex(@"\b\d+\s*(pcs|pieces)\b|\b\d+\s*[~-]\s*\d+\s*pcs\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmbiguousQuantityPattern();

    [GeneratedRegex(@"\b(pet\s+supplies|dog\s+toys?|cat\s+toys?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PetCategoryPattern();
}
