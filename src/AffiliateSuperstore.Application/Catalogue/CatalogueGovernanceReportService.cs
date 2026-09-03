using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record CatalogueGovernanceBrief(
    DateOnly DateUtc,
    int AutomaticDecisions,
    int ProductsPublished,
    int ProductsRetired,
    int ProductsHeld,
    int CollectionsPublished,
    int AiCalls,
    decimal AiSpendUsd,
    int AiFailures,
    int BudgetBlocks,
    int DeadLetters,
    int ApprovedButSuppressed,
    IReadOnlyList<string> Anomalies,
    DateTimeOffset GeneratedUtc)
{
    public bool IsHealthy => Anomalies.Count == 0;
}

public sealed class CatalogueGovernanceReportService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    CatalogueCollectionService collectionService,
    AutonomousCatalogueOptions autonomousOptions,
    TimeProvider timeProvider)
{
    public async Task<CatalogueGovernanceBrief> GetTodayAsync(
        string shopSlug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        var now = timeProvider.GetUtcNow();
        var start = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var shop = await context.Shops.AsNoTracking()
            .Where(item => item.Slug == shopSlug)
            .Select(item => new { item.Id, item.IsEnabled })
            .SingleAsync(cancellationToken);

        var decisions = await context.AutonomousCatalogueDecisions.AsNoTracking()
            .Where(item => item.ShopId == shop.Id &&
                item.Mode == AutonomousCatalogueMode.Automatic &&
                item.EvaluatedUtc >= start)
            .Select(item => new { item.Decision, item.Action })
            .ToArrayAsync(cancellationToken);
        var shopProductIds = context.ShopProducts
            .Where(item => item.ShopId == shop.Id)
            .Select(item => item.ProductId);
        var collectionSuggestionSubject = $"shop:{shopSlug}";
        var ai = await context.AiInvocations.AsNoTracking()
            .Where(item => item.RequestedUtc >= start &&
                (shopProductIds.Contains(item.ProductId) ||
                 (item.Purpose == AiInvocationAuditService.CollectionSuggestionPurpose &&
                  item.ProductId == collectionSuggestionSubject)))
            .Select(item => new
            {
                item.Status,
                item.ReservedCostUsd,
                item.EstimatedCostUsd
            })
            .ToArrayAsync(cancellationToken);
        var collectionsPublished = await context.CollectionPublicationEvents.AsNoTracking().CountAsync(item =>
            item.ShopId == shop.Id &&
            item.Mode == CollectionPublicationMode.Automatic &&
            item.Action == CollectionPublicationAction.Published &&
            item.OccurredUtc >= start,
            cancellationToken);
        var productsRetired = await context.ShopProducts.AsNoTracking().CountAsync(item =>
            item.ShopId == shop.Id &&
            item.ReviewStatus == ProductReviewStatus.Rejected &&
            item.DisabledReason != null &&
            item.DisabledReason.StartsWith(CatalogueAutonomousTriagePolicy.AutomaticRetirementReasonPrefix) &&
            item.AutomatedReviewedUtc >= start,
            cancellationToken);
        var deadLetters = await context.AutomationWorkItems.AsNoTracking().CountAsync(item =>
            item.ShopId == shop.Id && item.Status == AutomationWorkStatus.DeadLetter,
            cancellationToken);
        var approvedButSuppressed = await context.ShopProducts.AsNoTracking().CountAsync(item =>
            item.ShopId == shop.Id &&
            item.ReviewStatus == ProductReviewStatus.Approved &&
            (!item.IsActive ||
             !item.Product.IsEligible ||
             item.Product.AvailabilityState != ProductAvailabilityState.Available ||
             !item.Product.AffiliateLinks.Any(link =>
                 link.ShopId == shop.Id && link.Status == AffiliateLinkStatus.Active)),
            cancellationToken);
        var policyMode = await context.AutonomousCataloguePolicies.AsNoTracking()
            .Where(item => item.ShopId == shop.Id)
            .Select(item => (AutonomousCatalogueMode?)item.Mode)
            .SingleOrDefaultAsync(cancellationToken);
        var collections = await collectionService.GetCollectionsAsync(shopSlug, cancellationToken);
        var requiredCollectionProducts = Math.Clamp(
            autonomousOptions.MinimumAutomaticCollectionProducts,
            CatalogueCollectionService.MinimumIndexingTarget,
            CatalogueCollectionService.MaximumIndexingTarget);
        var readyPrivateCollections = collections.Count(item =>
            !item.IsPublished &&
            item.IndexableProducts >= Math.Max(requiredCollectionProducts, item.MinimumProductsForIndexing));

        var aiFailures = ai.Count(item => item.Status == AiInvocationStatus.Failed);
        var budgetBlocks = ai.Count(item => item.Status == AiInvocationStatus.BudgetBlocked);
        var anomalies = new List<string>();
        if (!shop.IsEnabled) anomalies.Add("The shop is disabled.");
        if (policyMode != AutonomousCatalogueMode.Automatic) anomalies.Add($"Autonomous policy is {policyMode?.ToString() ?? "missing"}, not Automatic.");
        if (deadLetters > 0) anomalies.Add($"{deadLetters} automation work item{(deadLetters == 1 ? " is" : "s are")} in the dead-letter queue.");
        if (aiFailures > 0) anomalies.Add($"{aiFailures} AI call{(aiFailures == 1 ? " has" : "s have")} failed today.");
        if (budgetBlocks > 0) anomalies.Add($"{budgetBlocks} AI call{(budgetBlocks == 1 ? " was" : "s were")} blocked by budget today.");
        if (approvedButSuppressed > 0) anomalies.Add($"{approvedButSuppressed} approved product{(approvedButSuppressed == 1 ? " is" : "s are")} suppressed by a current availability, eligibility or affiliate-link gate.");
        if (readyPrivateCollections > 0) anomalies.Add($"{readyPrivateCollections} collection{(readyPrivateCollections == 1 ? " has" : "s have")} reached its product threshold but remains private.");

        return new CatalogueGovernanceBrief(
            DateOnly.FromDateTime(start.UtcDateTime),
            decisions.Length,
            decisions.Count(item => item.Action == AutonomousCatalogueAction.Published),
            productsRetired,
            decisions.Count(item => item.Decision == AutonomousCatalogueDecision.Hold),
            collectionsPublished,
            ai.Length,
            ai.Sum(item => item.Status == AiInvocationStatus.Reserved
                ? item.ReservedCostUsd
                : item.Status == AiInvocationStatus.CacheHit || item.Status == AiInvocationStatus.BudgetBlocked
                    ? 0m
                    : item.EstimatedCostUsd),
            aiFailures,
            budgetBlocks,
            deadLetters,
            approvedButSuppressed,
            anomalies,
            now);
    }
}
