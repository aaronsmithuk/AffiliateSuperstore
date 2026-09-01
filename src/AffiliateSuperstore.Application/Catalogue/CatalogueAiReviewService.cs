using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record CatalogueAiReviewInvocation(
    Guid Id,
    string ProductId,
    string SourceTitle,
    string Provider,
    string Model,
    string PromptVersion,
    AiInvocationStatus Status,
    EditorialValidationState ValidationState,
    IReadOnlyList<EditorialValidationFinding> Findings,
    DateTimeOffset RequestedUtc,
    DateTimeOffset? CompletedUtc,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    long? LatencyMilliseconds,
    bool WasCached,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record CatalogueAiReviewItem(
    string ProductId,
    string SourceTitle,
    string? ProductDetailUrl,
    string? ImageUrl,
    string? FirstLevelCategoryName,
    string? SecondLevelCategoryName,
    string? SellerName,
    decimal? SalePrice,
    decimal? OriginalPrice,
    string Currency,
    decimal? EvaluationRate,
    long? RecentSalesVolume,
    IReadOnlyList<string> Collections,
    string? EditorialTitle,
    string? EditorialDescription,
    bool IsFeatured,
    int DisplayOrder,
    ProductReviewStatus ReviewStatus,
    EditorialValidationState ValidationState,
    IReadOnlyList<EditorialValidationFinding> ValidationFindings,
    IReadOnlyList<ProductQualityFlag> QualityFlags,
    bool IsActive,
    bool IsEligible,
    ProductAvailabilityState AvailabilityState,
    bool HasActiveLink,
    int CurrentVersionNumber,
    int AiDraftVersionNumber,
    string AiChangeReason,
    string AiCreatedBy,
    DateTimeOffset AiCreatedUtc,
    string ExpectedRowVersion,
    CatalogueAiReviewInvocation? Invocation)
{
    public bool WasHumanEdited => CurrentVersionNumber > AiDraftVersionNumber;
    public bool IsAwaitingReview => ReviewStatus is ProductReviewStatus.Pending or ProductReviewStatus.NeedsReview;
    public bool CanApprove =>
        IsActive &&
        IsEligible &&
        HasActiveLink &&
        QualityFlags.Count == 0 &&
        ValidationState == EditorialValidationState.Passed;
}

public sealed record CatalogueAiReviewDashboard(
    IReadOnlyList<CatalogueAiReviewItem> Items,
    IReadOnlyList<CatalogueAiReviewInvocation> RecentInvocations)
{
    public int AwaitingReview => Items.Count(item => item.IsAwaitingReview);
    public int Approved => Items.Count(item => item.ReviewStatus == ProductReviewStatus.Approved);
    public int Rejected => Items.Count(item => item.ReviewStatus == ProductReviewStatus.Rejected);
    public int WarningInvocations => RecentInvocations.Count(item => item.ValidationState == EditorialValidationState.Warning);
    public int BlockedInvocations => RecentInvocations.Count(item => item.ValidationState == EditorialValidationState.Blocked);
    public decimal RecentEstimatedCostUsd => RecentInvocations.Sum(item => item.EstimatedCostUsd);
}

public sealed class CatalogueAiReviewService(IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory)
{
    public const int MaximumReviewItems = 200;
    public const int MaximumRecentInvocations = 100;
    public const string AiDraftReasonPrefix = "AI-assisted review draft (";

    public async Task<CatalogueAiReviewDashboard> GetAsync(
        string shopSlug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var products = await context.ShopProducts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.Shop)
            .Include(item => item.Product).ThenInclude(product => product.Snapshots)
            .Include(item => item.Product).ThenInclude(product => product.Collections).ThenInclude(item => item.Collection)
            .Include(item => item.Product).ThenInclude(product => product.AffiliateLinks)
            .Include(item => item.EditorialVersions)
            .Where(item => item.Shop.Slug == shopSlug &&
                item.EditorialVersions.Any(version =>
                    version.ChangeReason != null && version.ChangeReason.StartsWith(AiDraftReasonPrefix)))
            .OrderBy(item => item.ReviewStatus == ProductReviewStatus.NeedsReview ? 0 :
                item.ReviewStatus == ProductReviewStatus.Pending ? 1 :
                item.ReviewStatus == ProductReviewStatus.Approved ? 2 : 3)
            .ThenByDescending(item => item.EditorialVersions.Max(version => version.CreatedUtc))
            .Take(MaximumReviewItems)
            .ToListAsync(cancellationToken);

        var referencedInvocationIds = products
            .Select(item => item.EditorialVersions
                .Where(version => version.ChangeReason?.StartsWith(AiDraftReasonPrefix, StringComparison.Ordinal) == true)
                .OrderByDescending(version => version.VersionNumber)
                .Select(version => TryReadInvocationId(version.ChangeReason))
                .FirstOrDefault())
            .OfType<Guid>()
            .Distinct()
            .ToArray();
        var recentInvocations = await context.AiInvocations
            .AsNoTracking()
            .Where(item => item.Purpose == AiInvocationAuditService.ProductCopyPurpose)
            .OrderByDescending(item => item.RequestedUtc)
            .Take(MaximumRecentInvocations)
            .ToListAsync(cancellationToken);
        var missingInvocationIds = referencedInvocationIds
            .Except(recentInvocations.Select(item => item.Id))
            .ToArray();
        var referencedInvocations = missingInvocationIds.Length == 0
            ? []
            : await context.AiInvocations
                .AsNoTracking()
                .Where(item => missingInvocationIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
        var allInvocations = recentInvocations.Concat(referencedInvocations).ToArray();
        var productIds = allInvocations.Select(item => item.ProductId).Distinct().ToArray();
        var sourceTitles = await context.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.AliExpressProductId))
            .ToDictionaryAsync(product => product.AliExpressProductId, product => product.Title, cancellationToken);

        var invocationRows = allInvocations.Select(invocation => ToInvocation(
            invocation,
            sourceTitles.GetValueOrDefault(invocation.ProductId) ?? invocation.ProductId)).ToArray();
        var invocationById = invocationRows.ToDictionary(item => item.Id);
        var recentInvocationIds = recentInvocations.Select(item => item.Id).ToHashSet();
        var recentInvocationRows = invocationRows.Where(item => recentInvocationIds.Contains(item.Id)).ToArray();

        var items = products.Select(item =>
        {
            var aiVersion = item.EditorialVersions
                .Where(version => version.ChangeReason?.StartsWith(AiDraftReasonPrefix, StringComparison.Ordinal) == true)
                .OrderByDescending(version => version.VersionNumber)
                .First();
            var currentVersion = item.EditorialVersions
                .OrderByDescending(version => version.VersionNumber)
                .First();
            var snapshot = item.Product.Snapshots.OrderByDescending(value => value.FetchedUtc).FirstOrDefault();
            var invocationId = TryReadInvocationId(aiVersion.ChangeReason);
            invocationById.TryGetValue(invocationId ?? Guid.Empty, out var invocation);

            return new CatalogueAiReviewItem(
                item.ProductId,
                item.Product.Title,
                item.Product.ProductDetailUrl,
                item.Product.MainImageUrl,
                item.Product.FirstLevelCategoryName,
                item.Product.SecondLevelCategoryName,
                item.Product.SellerName,
                snapshot?.SalePrice,
                snapshot?.OriginalPrice,
                snapshot?.Currency ?? "GBP",
                snapshot?.EvaluationRate,
                snapshot?.RecentSalesVolume,
                item.Product.Collections
                    .Where(collection => collection.Collection.ShopId == item.ShopId)
                    .OrderBy(collection => collection.Collection.DisplayOrder)
                    .ThenBy(collection => collection.Collection.DisplayName)
                    .Select(collection => collection.Collection.DisplayName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                item.EditorialTitle,
                item.EditorialDescription,
                item.IsFeatured,
                item.DisplayOrder,
                item.ReviewStatus,
                currentVersion.ValidationState,
                EditorialContentValidator.ReadFindings(currentVersion.ValidationFindingsJson),
                ProductQualityAssessmentService.ReadFlags(item.AutomatedReviewFlags),
                item.IsActive,
                item.Product.IsEligible,
                item.Product.AvailabilityState,
                item.Product.AffiliateLinks.Any(link =>
                    link.ShopId == item.ShopId && link.Status == AffiliateLinkStatus.Active),
                currentVersion.VersionNumber,
                aiVersion.VersionNumber,
                aiVersion.ChangeReason ?? AiDraftReasonPrefix,
                aiVersion.CreatedBy,
                aiVersion.CreatedUtc,
                Convert.ToBase64String(item.RowVersion),
                invocation);
        }).ToArray();

        return new CatalogueAiReviewDashboard(items, recentInvocationRows);
    }

    public static Guid? TryReadInvocationId(string? changeReason)
    {
        if (string.IsNullOrWhiteSpace(changeReason)) return null;
        const string marker = "invocation ";
        var markerIndex = changeReason.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0) return null;
        var valueStart = markerIndex + marker.Length;
        var valueEnd = changeReason.IndexOf(')', valueStart);
        var value = valueEnd < 0 ? changeReason[valueStart..] : changeReason[valueStart..valueEnd];
        return Guid.TryParse(value.Trim(), out var invocationId) ? invocationId : null;
    }

    private static CatalogueAiReviewInvocation ToInvocation(AiInvocationRecord item, string sourceTitle) => new(
        item.Id,
        item.ProductId,
        sourceTitle,
        item.Provider,
        item.Model,
        item.PromptVersion,
        item.Status,
        item.EditorialValidationState,
        EditorialContentValidator.ReadFindings(item.ValidationFindingsJson),
        item.RequestedUtc,
        item.CompletedUtc,
        item.InputTokens ?? 0,
        item.OutputTokens ?? 0,
        item.EstimatedCostUsd,
        item.LatencyMilliseconds,
        item.Status == AiInvocationStatus.CacheHit,
        item.ErrorCode,
        item.ErrorMessage);
}
