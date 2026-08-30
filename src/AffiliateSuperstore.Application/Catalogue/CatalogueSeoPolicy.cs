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
        DateTimeOffset lastCheckedUtc)
    {
        var titleLength = editorialTitle?.Trim().Length ?? 0;
        var descriptionLength = editorialDescription?.Trim().Length ?? 0;
        return titleLength is >= MinimumEditorialTitleLength and <= MaximumEditorialTitleLength &&
            descriptionLength >= MinimumEditorialDescriptionLength &&
            !string.IsNullOrWhiteSpace(imageUrl) &&
            price > 0 &&
            lastCheckedUtc >= timeProvider.GetUtcNow().AddDays(-MaximumSnapshotAgeDays);
    }

    public static bool IsShopIndexable(int indexableProductCount) =>
        indexableProductCount >= MinimumIndexableProductsPerShop;
}
