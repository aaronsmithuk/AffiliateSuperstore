using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record CatalogueReadinessReport(
    string ShopSlug,
    int ActiveProducts,
    int QualityClearProducts,
    int FlaggedProducts,
    int PendingProducts,
    int NeedsReviewProducts,
    int ApprovedProducts,
    int RejectedProducts,
    int ProductsWithActiveLinks,
    int EditoriallyCompleteProducts,
    int IndexableProducts,
    int StaleProducts,
    DateTimeOffset? LastCatalogueRefreshUtc)
{
    public int IndexingTarget => CatalogueSeoPolicy.MinimumIndexableProductsPerShop;
    public int ProductsNeededForIndexing => Math.Max(0, IndexingTarget - IndexableProducts);
    public bool ShopIsIndexable => CatalogueSeoPolicy.IsShopIndexable(IndexableProducts);
}

public sealed class CatalogueReadinessService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    CatalogueSeoPolicy seoPolicy,
    TimeProvider timeProvider)
{
    public async Task<CatalogueReadinessReport> GetAsync(
        string shopSlug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var products = await context.ShopProducts.AsNoTracking()
            .Where(item => item.Shop.Slug == shopSlug && item.IsActive)
            .Select(item => new ReadinessRow(
                item.Product.IsEligible,
                item.ReviewStatus,
                item.AutomatedReviewFlags,
                item.EditorialTitle,
                item.EditorialDescription,
                item.Product.MainImageUrl,
                item.Product.LastRefreshedUtc,
                item.Product.Snapshots
                    .OrderByDescending(snapshot => snapshot.FetchedUtc)
                    .Select(snapshot => snapshot.SalePrice)
                    .FirstOrDefault(),
                item.Product.AffiliateLinks.Any(link =>
                    link.ShopId == item.ShopId && link.Status == AffiliateLinkStatus.Active)))
            .ToListAsync(cancellationToken);
        var staleBefore = timeProvider.GetUtcNow().AddDays(-CatalogueSeoPolicy.MaximumSnapshotAgeDays);
        var clear = products.Where(item => ProductQualityAssessmentService.ReadFlags(item.Flags).Count == 0).ToArray();
        var indexable = clear.Count(item =>
            item.IsEligible &&
            item.HasActiveLink &&
            item.ReviewStatus == ProductReviewStatus.Approved &&
            seoPolicy.IsProductIndexable(
                item.EditorialTitle,
                item.EditorialDescription,
                item.ImageUrl,
                item.Price,
                item.LastRefreshedUtc));

        return new CatalogueReadinessReport(
            shopSlug,
            products.Count,
            clear.Length,
            products.Count - clear.Length,
            products.Count(item => item.ReviewStatus == ProductReviewStatus.Pending),
            products.Count(item => item.ReviewStatus == ProductReviewStatus.NeedsReview),
            products.Count(item => item.ReviewStatus == ProductReviewStatus.Approved),
            products.Count(item => item.ReviewStatus == ProductReviewStatus.Rejected),
            products.Count(item => item.HasActiveLink),
            products.Count(item =>
                (item.EditorialTitle?.Trim().Length ?? 0) is >= CatalogueSeoPolicy.MinimumEditorialTitleLength and <= CatalogueSeoPolicy.MaximumEditorialTitleLength &&
                (item.EditorialDescription?.Trim().Length ?? 0) >= CatalogueSeoPolicy.MinimumEditorialDescriptionLength),
            indexable,
            products.Count(item => item.LastRefreshedUtc < staleBefore),
            products.Count == 0 ? null : products.Max(item => item.LastRefreshedUtc));
    }

    private sealed record ReadinessRow(
        bool IsEligible,
        ProductReviewStatus ReviewStatus,
        string? Flags,
        string? EditorialTitle,
        string? EditorialDescription,
        string? ImageUrl,
        DateTimeOffset LastRefreshedUtc,
        decimal? Price,
        bool HasActiveLink);
}
