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
        (LicensingClaimPattern(), new("ip.licensing-claim", "Title claims an original, genuine or official branded product; verify the seller and licensing before publication.")),
        (TobaccoPattern(), new("safety.tobacco-themed", "Tobacco-themed product is unsuitable for the initial shop.")),
        (AmbiguousQuantityPattern(), new("listing.ambiguous-quantity", "Listing advertises multiple possible quantities; check that the displayed price is not misleading.")),
        (VariantSizePattern(), new("listing.variant-dependent-price", "Listing advertises multiple sizes; verify that the displayed price represents the pictured option.")),
        (NonPlushProductPattern(), new("scope.non-plush-product", "Listing appears to be an accessory, material or non-plush product outside the initial catalogue focus."))
    ];

    public ProductQualityAssessment Assess(
        string title,
        string? firstLevelCategoryName = null,
        string? secondLevelCategoryName = null,
        bool requirePlushEvidence = false)
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
        if (requirePlushEvidence &&
            !PlushEvidencePattern().IsMatch(title) &&
            !PlushEvidencePattern().IsMatch(categories))
        {
            flags.Add(new ProductQualityFlag(
                "scope.missing-plush-evidence",
                "Neither the title nor AliExpress category identifies this as a plush or stuffed product."));
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
        var sourceAssessment = Assess(
            sourceTitle,
            firstLevelCategoryName,
            secondLevelCategoryName,
            requirePlushEvidence: true);
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

    [GeneratedRegex(@"\b(catnip|dog\b.{0,20}\btoys?|cat\b.{0,20}\btoys?|pet\s+toys?|for\s+cats|for\s+dogs|teeth\s+grinding|pet\s+interactive)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PetProductPattern();

    [GeneratedRegex(@"\b(newborn|bab(?:y|ies)|baby\s+rattle|stroller|crib|bassinet|teether|cot\s+mobile)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BabyProductPattern();

    [GeneratedRegex(@"\b(mimikyu|eevee|pokemon|pokémon|fnaf|five\s+nights?\s+at\s+freddy|freddy|jeffy|sml\s+puppet|michael\s+jackson|domo\s+kun|hello\s+kitty|sanrio|kuromi|cinnamoroll|my\s+melody|pompompurin|pochacco|disney|stitch|lilo|marvel|star\s+wars|harry\s+potter|totoro|ghibli|kirby|mario|luigi|sonic|minecraft|creeper|poppy\s+playtime|huggy\s+wuggy|squishmallows?|care\s+bears?|winnie|pooh|one\s+piece|naruto|dragon\s+ball|demon\s+slayer|genshin|honkai|hazbin|labubu|pop\s+mart|minions?|garfield|snoopy|miffy|rilakkuma|pusheen|kermits?|smiling\s+friends|om\s+nom|cut\s+the\s+rope|bad\s+bunny|kinitopet|crayon\s+shinchan|shinchan|anime|game|cosplay)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ThirdPartyCharacterPattern();

    [GeneratedRegex(@"\b(official|genuine|original)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LicensingClaimPattern();

    [GeneratedRegex(@"\b(cigar|cigarette|tobacco)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TobaccoPattern();

    [GeneratedRegex(@"\b\d+\s*(pcs|pieces|sets)\b|\b\d+\s*[~-]\s*\d+\s*(pcs|pieces|sets)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmbiguousQuantityPattern();

    [GeneratedRegex(@"\b\d+\s*[-/]\s*\d+\s*(cm|inches?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VariantSizePattern();

    [GeneratedRegex(@"\b(plush\s+fabric|(?:diy\s+)?(?:sewing|craft)\s+(?:kit|set)|doll\s+(shoes|slippers|accessories)|squeeze\s+ball|squishy\s+stress|plush\s+(backpack|crossbody|shoulder\s+bag|pencil\s+case|coin\s+purse)|slap\s+snap\s+wrap|wristband\s+bracelet)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NonPlushProductPattern();

    [GeneratedRegex(@"\b(pet\s+supplies|dog\s+toys?|cat\s+toys?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PetCategoryPattern();

    [GeneratedRegex(@"\b(plush(?:ie|y|ies)?|stuffed|soft\s+(?:toy|doll)|cuddly|teddy|puppets?|rag\s+doll|peluche)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlushEvidencePattern();
}
