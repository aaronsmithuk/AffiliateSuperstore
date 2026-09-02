using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record CatalogueAiQueuePreparationItem(
    string ProductId,
    string SourceTitle,
    ProductReviewStatus ReviewStatus,
    CatalogueAiSuggestionResult SuggestionResult,
    bool DraftSaved,
    Guid? EditorialVersionId,
    decimal EstimatedCostUsd,
    string Outcome);

public sealed record CatalogueAiQueuePreparationResult(
    int RequestedCount,
    int SelectedCount,
    int CompletedCount,
    int DraftsSaved,
    int WarningCount,
    int BlockedCount,
    int FailedCount,
    int CacheHitCount,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    IReadOnlyList<CatalogueAiQueuePreparationItem> Items,
    string Message);

public sealed class CatalogueAiQueuePreparationService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    CatalogueAiSuggestionService suggestionService,
    CatalogueEditorialService editorialService,
    ProductQualityAssessmentService qualityAssessmentService,
    AiAutomationOptions options)
{
    public const int MaximumBatchSize = 10;
    private const int CandidatePoolMultiplier = 12;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<CatalogueAiQueuePreparationResult> RunAsync(
        string shopSlug,
        int requestedCount = MaximumBatchSize,
        string actor = "administrator",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        var batchSize = Math.Clamp(requestedCount, 1, MaximumBatchSize);
        if (!await Gate.WaitAsync(0, cancellationToken))
        {
            return Empty(batchSize, "Another AI approval-queue preparation run is already in progress.");
        }

        try
        {
            if (!options.IsAvailable)
            {
                return Empty(batchSize, options.AvailabilityMessage);
            }

            var candidates = await SelectCandidatesAsync(shopSlug, batchSize, cancellationToken);
            if (candidates.Count == 0)
            {
                return Empty(batchSize,
                    "No collection-assigned, quality-clear products with active links and empty editorial copy are waiting for AI preparation.");
            }

            var items = new List<CatalogueAiQueuePreparationItem>(candidates.Count);
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var suggestionResult = await suggestionService.SuggestAsync(shopSlug, candidate.ProductId, cancellationToken);
                var suggestion = suggestionResult.Suggestion;
                var estimatedCost = options.EstimateCostUsd(
                    suggestion?.InputTokens ?? 0,
                    suggestion?.OutputTokens ?? 0);

                if (!suggestionResult.Succeeded || suggestion is null)
                {
                    items.Add(new CatalogueAiQueuePreparationItem(
                        candidate.ProductId,
                        candidate.SourceTitle,
                        candidate.ReviewStatus,
                        suggestionResult,
                        false,
                        null,
                        estimatedCost,
                        suggestionResult.Message));
                    continue;
                }

                var hasWarnings = suggestionResult.Findings?.Count > 0;
                if (hasWarnings)
                {
                    items.Add(new CatalogueAiQueuePreparationItem(
                        candidate.ProductId,
                        candidate.SourceTitle,
                        candidate.ReviewStatus,
                        suggestionResult,
                        false,
                        null,
                        estimatedCost,
                        "The AI draft has validation warnings and was left unsaved for manual handling."));
                    continue;
                }

                var saveResult = await editorialService.SaveAsync(new CatalogueEditorialUpdate(
                    shopSlug,
                    candidate.ProductId,
                    suggestion.SuggestedTitle,
                    suggestion.SuggestedDescription,
                    candidate.IsFeatured,
                    candidate.DisplayOrder,
                    candidate.ExpectedRowVersion,
                    NormaliseActor(actor),
                    BuildChangeReason(suggestion)), cancellationToken);

                items.Add(new CatalogueAiQueuePreparationItem(
                    candidate.ProductId,
                    candidate.SourceTitle,
                    candidate.ReviewStatus,
                    suggestionResult,
                    saveResult.Succeeded,
                    saveResult.VersionId,
                    estimatedCost,
                    saveResult.Succeeded
                        ? "Validated AI copy was saved as a review draft. Approval is still required."
                        : $"The validated AI draft could not be saved: {saveResult.Message}"));
            }

            return BuildResult(batchSize, candidates.Count, items);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<IReadOnlyList<QueueCandidate>> SelectCandidatesAsync(
        string shopSlug,
        int batchSize,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var pool = await context.ShopProducts
            .AsNoTracking()
            .Where(item => item.Shop.Slug == shopSlug &&
                item.IsActive &&
                item.Product.IsEligible &&
                item.Product.AvailabilityState == ProductAvailabilityState.Available &&
                (item.ReviewStatus == ProductReviewStatus.Pending || item.ReviewStatus == ProductReviewStatus.NeedsReview) &&
                (item.EditorialTitle == null || item.EditorialTitle == "") &&
                (item.EditorialDescription == null || item.EditorialDescription == "") &&
                context.AffiliateLinks.Any(link =>
                    link.ShopId == item.ShopId &&
                    link.ProductId == item.ProductId &&
                    link.Status == AffiliateLinkStatus.Active) &&
                context.CollectionProducts.Any(membership =>
                    membership.ProductId == item.ProductId &&
                    membership.Collection.ShopId == item.ShopId &&
                    membership.Collection.IsPublished))
            .OrderBy(item => item.ReviewStatus == ProductReviewStatus.Pending ? 0 : 1)
            .ThenByDescending(item => context.CollectionProducts.Count(membership =>
                membership.ProductId == item.ProductId &&
                membership.Collection.ShopId == item.ShopId))
            .ThenByDescending(item => item.Product.LastRefreshedUtc)
            .ThenBy(item => item.ProductId)
            .Take(batchSize * CandidatePoolMultiplier)
            .Select(item => new QueueCandidate(
                item.ProductId,
                item.Product.Title,
                item.Product.FirstLevelCategoryName,
                item.Product.SecondLevelCategoryName,
                item.Product.SellerName,
                item.Product.SkuId,
                item.Product.EanCode,
                item.ReviewStatus,
                item.IsFeatured,
                item.DisplayOrder,
                item.RowVersion))
            .ToListAsync(cancellationToken);

        var qualityClear = pool
            .Where(candidate => !qualityAssessmentService.AssessForPublication(
                candidate.SourceTitle,
                null,
                candidate.FirstLevelCategoryName,
                candidate.SecondLevelCategoryName).RequiresReview)
            .ToArray();
        if (qualityClear.Length == 0) return [];

        var productIds = qualityClear.Select(candidate => candidate.ProductId).ToArray();
        var rejectedInputs = await context.AiInvocations.AsNoTracking()
            .Where(invocation => productIds.Contains(invocation.ProductId) &&
                invocation.Purpose == AiInvocationAuditService.ProductCopyPurpose &&
                invocation.PromptVersion == CatalogueAiSuggestionService.PromptVersion &&
                (invocation.Status == AiInvocationStatus.Succeeded || invocation.Status == AiInvocationStatus.CacheHit) &&
                invocation.EditorialValidationState != EditorialValidationState.Passed)
            .Select(invocation => new { invocation.ProductId, invocation.InputHash })
            .ToArrayAsync(cancellationToken);
        var rejectedHashesByProduct = rejectedInputs
            .GroupBy(invocation => invocation.ProductId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(invocation => invocation.InputHash).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

        return qualityClear
            .Where(candidate => !rejectedHashesByProduct.TryGetValue(candidate.ProductId, out var hashes) ||
                !hashes.Contains(candidate.InputHash))
            .Take(batchSize)
            .ToArray();
    }

    private static CatalogueAiQueuePreparationResult BuildResult(
        int requestedCount,
        int selectedCount,
        IReadOnlyList<CatalogueAiQueuePreparationItem> items)
    {
        var saved = items.Count(item => item.DraftSaved);
        var warnings = items.Count(item =>
            item.SuggestionResult.Succeeded &&
            item.SuggestionResult.Findings?.Count > 0);
        var blocked = items.Count(item => item.SuggestionResult.IsBlocked);
        var failed = items.Count(item =>
            (!item.SuggestionResult.Succeeded && !item.SuggestionResult.IsBlocked) ||
            (item.SuggestionResult.Succeeded &&
                item.SuggestionResult.Findings?.Count is not > 0 &&
                !item.DraftSaved));
        return new CatalogueAiQueuePreparationResult(
            requestedCount,
            selectedCount,
            items.Count,
            saved,
            warnings,
            blocked,
            failed,
            items.Count(item => item.SuggestionResult.Suggestion?.WasCached == true),
            items.Sum(item => item.SuggestionResult.Suggestion?.InputTokens ?? 0),
            items.Sum(item => item.SuggestionResult.Suggestion?.OutputTokens ?? 0),
            items.Sum(item => item.EstimatedCostUsd),
            items,
            $"Prepared {saved} review draft{(saved == 1 ? "" : "s")}. Nothing was approved or published automatically.");
    }

    private static CatalogueAiQueuePreparationResult Empty(int requestedCount, string message) =>
        new(requestedCount, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0m, [], message);

    private static string NormaliseActor(string actor)
    {
        var normalised = string.Join(' ', actor.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var decorated = string.IsNullOrWhiteSpace(normalised)
            ? "AI queue preparation"
            : $"{normalised} via AI queue";
        return decorated.Length <= CatalogueEditorialService.MaximumActorLength
            ? decorated
            : decorated[..CatalogueEditorialService.MaximumActorLength];
    }

    private static string BuildChangeReason(ProductEditorialSuggestionOutput suggestion)
    {
        var reason = $"AI-assisted review draft ({suggestion.Provider}/{suggestion.Model}, {CatalogueAiSuggestionService.PromptVersion}, invocation {suggestion.InvocationId?.ToString() ?? "cache"}); requires administrator approval.";
        return reason.Length <= CatalogueEditorialService.MaximumChangeReasonLength
            ? reason
            : reason[..CatalogueEditorialService.MaximumChangeReasonLength];
    }

    private sealed record QueueCandidate(
        string ProductId,
        string SourceTitle,
        string? FirstLevelCategoryName,
        string? SecondLevelCategoryName,
        string? SellerName,
        string? SkuId,
        string? EanCode,
        ProductReviewStatus ReviewStatus,
        bool IsFeatured,
        int DisplayOrder,
        byte[] RowVersion)
    {
        public string ExpectedRowVersion => Convert.ToBase64String(RowVersion);
        public string InputHash => CatalogueAiSuggestionService.ComputeInputHash(
            ProductId,
            SourceTitle,
            null,
            null,
            CatalogueAiSuggestionService.BuildFacts(
                SourceTitle,
                FirstLevelCategoryName,
                SecondLevelCategoryName,
                SellerName,
                SkuId,
                EanCode));
    }
}
