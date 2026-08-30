using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record CatalogueEditorialUpdate(
    string ShopSlug,
    string ProductId,
    string? EditorialTitle,
    string? EditorialDescription,
    bool IsFeatured,
    int DisplayOrder);

public sealed record CatalogueCommandResult(bool Succeeded, string Message);

public sealed class CatalogueEditorialService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    ProductQualityAssessmentService qualityAssessmentService,
    TimeProvider timeProvider)
{
    public const int MaximumTitleLength = 180;
    public const int MaximumDescriptionLength = 1000;
    public const int MaximumDisplayOrder = 10_000;

    public async Task<CatalogueCommandResult> SaveAsync(
        CatalogueEditorialUpdate update,
        CancellationToken cancellationToken = default)
    {
        var title = Normalise(update.EditorialTitle);
        var description = Normalise(update.EditorialDescription);
        if (title?.Length > MaximumTitleLength)
        {
            return Failure($"The editorial title must be {MaximumTitleLength} characters or fewer.");
        }
        if (description?.Length > MaximumDescriptionLength)
        {
            return Failure($"The editorial description must be {MaximumDescriptionLength} characters or fewer.");
        }
        if (update.DisplayOrder is < 0 or > MaximumDisplayOrder)
        {
            return Failure($"Display order must be between 0 and {MaximumDisplayOrder:N0}.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await FindProductAsync(context, update.ShopSlug, update.ProductId, cancellationToken);
        if (item is null) return Failure("The catalogue product could not be found.");

        item.EditorialTitle = title;
        item.EditorialDescription = description;
        item.IsFeatured = update.IsFeatured;
        item.DisplayOrder = update.DisplayOrder;
        ApplyQualityAssessment(item);
        if (item.ReviewStatus == ProductReviewStatus.Approved && HasQualityFlags(item))
        {
            item.ReviewStatus = ProductReviewStatus.NeedsReview;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Success(HasQualityFlags(item)
            ? "Editorial changes saved. The product remains in review because quality flags are present."
            : "Editorial changes saved.");
    }

    public async Task<CatalogueCommandResult> SetReviewStatusAsync(
        string shopSlug,
        string productId,
        ProductReviewStatus status,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await FindProductAsync(context, shopSlug, productId, cancellationToken);
        if (item is null) return Failure("The catalogue product could not be found.");

        if (status == ProductReviewStatus.Approved)
        {
            ApplyQualityAssessment(item);
            if (!item.IsActive || !item.Product.IsEligible)
            {
                HoldForReview(item);
                await context.SaveChangesAsync(cancellationToken);
                return Failure("This product is inactive or no longer eligible and cannot be published.");
            }
            if (HasQualityFlags(item))
            {
                HoldForReview(item);
                await context.SaveChangesAsync(cancellationToken);
                return Failure("Resolve or reject the product's quality flags before approving it.");
            }

            var hasActiveLink = await context.AffiliateLinks.AnyAsync(
                link => link.ShopId == item.ShopId &&
                    link.ProductId == item.ProductId &&
                    link.Status == AffiliateLinkStatus.Active,
                cancellationToken);
            if (!hasActiveLink)
            {
                HoldForReview(item);
                await context.SaveChangesAsync(cancellationToken);
                return Failure("Generate a working affiliate link before approving this product.");
            }
        }

        item.ReviewStatus = status;
        item.DisabledReason = status == ProductReviewStatus.Rejected
            ? "Rejected during catalogue review."
            : null;
        await context.SaveChangesAsync(cancellationToken);
        return Success(status switch
        {
            ProductReviewStatus.Approved => "Product approved and visible in the public shop.",
            ProductReviewStatus.Rejected => "Product rejected and removed from the public shop.",
            ProductReviewStatus.NeedsReview => "Product returned to the manual review queue.",
            _ => "Product review status updated."
        });
    }

    private void ApplyQualityAssessment(ShopProductRecord item)
    {
        var assessment = qualityAssessmentService.AssessForPublication(
            item.Product.Title,
            item.EditorialTitle,
            item.Product.FirstLevelCategoryName,
            item.Product.SecondLevelCategoryName);
        item.AutomatedReviewFlags = assessment.SerializedFlags;
        item.AutomatedReviewedUtc = timeProvider.GetUtcNow();
    }

    private static Task<ShopProductRecord?> FindProductAsync(
        AffiliateSuperstoreDbContext context,
        string shopSlug,
        string productId,
        CancellationToken cancellationToken) =>
        context.ShopProducts
            .Include(item => item.Shop)
            .Include(item => item.Product)
            .SingleOrDefaultAsync(
                item => item.Shop.Slug == shopSlug && item.ProductId == productId,
                cancellationToken);

    private static bool HasQualityFlags(ShopProductRecord item) =>
        ProductQualityAssessmentService.ReadFlags(item.AutomatedReviewFlags).Count > 0;

    private static void HoldForReview(ShopProductRecord item)
    {
        if (item.ReviewStatus != ProductReviewStatus.Rejected)
        {
            item.ReviewStatus = ProductReviewStatus.NeedsReview;
        }
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CatalogueCommandResult Success(string message) => new(true, message);
    private static CatalogueCommandResult Failure(string message) => new(false, message);
}
