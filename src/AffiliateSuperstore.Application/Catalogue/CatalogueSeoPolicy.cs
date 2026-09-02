namespace AffiliateSuperstore.Application.Catalogue;

public sealed class CatalogueSeoPolicy(TimeProvider timeProvider)
{
    public const int MinimumEditorialTitleLength = 8;
    public const int MaximumEditorialTitleLength = 90;
    public const int MinimumEditorialDescriptionLength = 60;
    public const int MinimumIndexableProductsPerShop = 12;
    public const int MaximumSnapshotAgeDays = 14;

    public bool IsProductIndexable(
        string? editorialTitle,
        string? editorialDescription,
        string? imageUrl,
        decimal? price,
        DateTimeOffset lastCheckedUtc) =>
        AssessProduct(editorialTitle, editorialDescription, imageUrl, price, lastCheckedUtc).IsIndexable;

    public CatalogueProductIndexingAssessment AssessProduct(
        string? editorialTitle,
        string? editorialDescription,
        string? imageUrl,
        decimal? price,
        DateTimeOffset lastCheckedUtc)
    {
        var titleLength = editorialTitle?.Trim().Length ?? 0;
        var descriptionLength = editorialDescription?.Trim().Length ?? 0;
        var issues = CatalogueProductIndexingIssue.None;
        if (titleLength is < MinimumEditorialTitleLength or > MaximumEditorialTitleLength)
        {
            issues |= CatalogueProductIndexingIssue.EditorialTitle;
        }
        if (descriptionLength < MinimumEditorialDescriptionLength)
        {
            issues |= CatalogueProductIndexingIssue.EditorialDescription;
        }
        if (string.IsNullOrWhiteSpace(imageUrl)) issues |= CatalogueProductIndexingIssue.Image;
        if (price is null or <= 0) issues |= CatalogueProductIndexingIssue.Price;
        if (lastCheckedUtc < timeProvider.GetUtcNow().AddDays(-MaximumSnapshotAgeDays))
        {
            issues |= CatalogueProductIndexingIssue.Freshness;
        }
        return new CatalogueProductIndexingAssessment(issues);
    }

    public static bool IsShopIndexable(int indexableProductCount) =>
        indexableProductCount >= MinimumIndexableProductsPerShop;
}

[Flags]
public enum CatalogueProductIndexingIssue
{
    None = 0,
    EditorialTitle = 1 << 0,
    EditorialDescription = 1 << 1,
    Image = 1 << 2,
    Price = 1 << 3,
    Freshness = 1 << 4
}

public sealed record CatalogueProductIndexingAssessment(CatalogueProductIndexingIssue Issues)
{
    public bool IsIndexable => Issues == CatalogueProductIndexingIssue.None;

    public bool Has(CatalogueProductIndexingIssue issue) => (Issues & issue) != 0;
}
