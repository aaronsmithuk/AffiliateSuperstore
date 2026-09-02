using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record CatalogueGrowthBlocker(string Code, string Label, int Products);

public sealed record CatalogueGrowthCollection(
    string Slug,
    string DisplayName,
    bool IsPublished,
    int AssignedProducts,
    int PublicProducts,
    int IndexableProducts,
    int TargetProducts,
    int ProductsNeeded,
    bool IsReadyToPublish);

public sealed record CatalogueGrowthCandidate(
    string ProductId,
    string Title,
    string? ImageUrl,
    IReadOnlyList<string> Collections,
    string Disposition,
    IReadOnlyList<string> ReasonCodes,
    DateTimeOffset DraftedUtc);

public sealed record CatalogueGrowthPipeline(
    int TargetPublicProducts,
    int PublicProducts,
    int ProductsStillNeeded,
    int AwaitingAiReview,
    int ReadyForAutonomousApproval,
    int PermanentRetirementCandidates,
    int AutomaticallyRetiredProducts,
    int DeferredByDailyLimit,
    int HeldForRepairOrReview,
    int DraftCollectionsReady,
    int? OptimisticDaysToTarget,
    int DailyPublicationLimit,
    IReadOnlyList<CatalogueGrowthCollection> Collections,
    IReadOnlyList<CatalogueGrowthBlocker> Blockers,
    IReadOnlyList<CatalogueGrowthCandidate> Candidates,
    DateTimeOffset GeneratedUtc);

public sealed class CatalogueGrowthPipelineService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    CatalogueAiReviewService reviewService,
    CatalogueCollectionService collectionService,
    AutonomousCataloguePolicyService policyService,
    TimeProvider timeProvider)
{
    public const int InitialPublicProductTarget = 50;
    public const int CollectionPublicationTarget = 12;

    public async Task<CatalogueGrowthPipeline> GetAsync(
        string shopSlug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        var now = timeProvider.GetUtcNow();
        var dashboardTask = reviewService.GetAsync(shopSlug, cancellationToken);
        var collectionsTask = collectionService.GetCollectionsAsync(shopSlug, cancellationToken);
        var policyTask = policyService.GetAsync(shopSlug, cancellationToken);
        await Task.WhenAll(dashboardTask, collectionsTask, policyTask);

        var dashboard = await dashboardTask;
        var collections = await collectionsTask;
        var policy = await policyTask;
        var awaiting = dashboard.Items.Where(item => item.IsAwaitingReview).ToArray();
        var productIds = awaiting.Select(item => item.ProductId).Distinct(StringComparer.Ordinal).ToArray();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var shopId = policy?.ShopId ?? await context.Shops.AsNoTracking()
            .Where(item => item.Slug == shopSlug)
            .Select(item => item.Id)
            .SingleAsync(cancellationToken);
        var publicProducts = await context.ShopProducts.AsNoTracking().CountAsync(item =>
            item.ShopId == shopId &&
            item.IsActive &&
            item.ReviewStatus == ProductReviewStatus.Approved &&
            item.Product.IsEligible &&
            item.Product.AvailabilityState == ProductAvailabilityState.Available &&
            item.Product.AffiliateLinks.Any(link =>
                link.ShopId == shopId && link.Status == AffiliateLinkStatus.Active), cancellationToken);
        var automaticallyRetired = await context.ShopProducts.AsNoTracking().CountAsync(item =>
            item.ShopId == shopId &&
            item.ReviewStatus == ProductReviewStatus.Rejected &&
            item.DisabledReason != null &&
            item.DisabledReason.StartsWith(CatalogueAutonomousTriagePolicy.AutomaticRetirementReasonPrefix),
            cancellationToken);
        var productsInPublishedCollections = productIds.Length == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await context.CollectionProducts.AsNoTracking()
                .Where(item => productIds.Contains(item.ProductId) &&
                    item.Collection.ShopId == shopId &&
                    item.Collection.IsPublished)
                .Select(item => item.ProductId)
                .Distinct()
                .ToArrayAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);
        var duplicateRows = productIds.Length == 0
            ? []
            : await context.ProductMatchCandidates.AsNoTracking()
                .Where(item => item.IsCurrent &&
                    item.ReviewStatus == ProductMatchReviewStatus.Pending &&
                    item.SuggestedRelationship == ProductRelationship.Duplicate &&
                    (productIds.Contains(item.LeftProductId) || productIds.Contains(item.RightProductId)))
                .Select(item => new { item.LeftProductId, item.RightProductId, item.Confidence })
                .ToArrayAsync(cancellationToken);
        var probableDuplicates = productIds.ToDictionary(
            productId => productId,
            productId => duplicateRows.Any(item =>
                (item.LeftProductId == productId || item.RightProductId == productId) &&
                item.Confidence >= (policy?.DuplicateHoldConfidence ?? .75m)),
            StringComparer.Ordinal);
        var dailyLimitRows = productIds.Length == 0
            ? []
            : await context.AutonomousCatalogueDecisions.AsNoTracking()
                .Where(item => item.ShopId == shopId &&
                    productIds.Contains(item.ProductId) &&
                    item.EvaluatedUtc >= StartOfUtcDay(now) &&
                    item.ReasonCodesJson.Contains("publication.daily-limit"))
                .Select(item => item.ProductId)
                .Distinct()
                .ToArrayAsync(cancellationToken);
        var deferred = dailyLimitRows.ToHashSet(StringComparer.Ordinal);

        var candidateRows = new List<CatalogueGrowthCandidate>(awaiting.Length);
        var blockers = new Dictionary<string, int>(StringComparer.Ordinal);
        var ready = 0;
        var retirement = 0;
        foreach (var item in awaiting)
        {
            var reasons = new List<string>();
            var retirementReasons = CatalogueAutonomousTriagePolicy.RetirementReasons(item.QualityFlags);
            reasons.AddRange(item.QualityFlags.Select(flag => flag.Code));
            reasons.AddRange(item.ValidationFindings.Select(finding => $"editorial.{finding.Code}"));
            if (!item.IsActive) reasons.Add("product.inactive");
            if (!item.IsEligible) reasons.Add("product.ineligible");
            if (item.AvailabilityState != ProductAvailabilityState.Available) reasons.Add("product.unavailable");
            if (!item.HasActiveLink) reasons.Add("affiliate-link.missing");
            if (!productsInPublishedCollections.Contains(item.ProductId)) reasons.Add("published-collection.missing");
            if (probableDuplicates.GetValueOrDefault(item.ProductId)) reasons.Add("duplicate.probable");
            if (deferred.Contains(item.ProductId)) reasons.Add("publication.daily-limit");
            reasons = reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

            string disposition;
            if (retirementReasons.Count > 0)
            {
                disposition = "Permanent retirement";
                retirement++;
            }
            else if (reasons.Count == 0 && item.CanApprove)
            {
                disposition = "Ready for autonomous approval";
                ready++;
            }
            else if (deferred.Contains(item.ProductId) && reasons.All(code => code == "publication.daily-limit"))
            {
                disposition = "Deferred until next UTC day";
            }
            else
            {
                disposition = "Held for repair or review";
            }

            foreach (var reason in reasons)
            {
                blockers[reason] = blockers.GetValueOrDefault(reason) + 1;
            }
            candidateRows.Add(new CatalogueGrowthCandidate(
                item.ProductId,
                item.EditorialTitle ?? item.SourceTitle,
                item.ImageUrl,
                item.Collections,
                disposition,
                reasons,
                item.AiCreatedUtc));
        }

        var productsStillNeeded = Math.Max(0, InitialPublicProductTarget - publicProducts);
        var dailyLimit = policy?.MaximumAutoPublishesPerDay ?? 0;
        int? daysToTarget = productsStillNeeded == 0
            ? 0
            : dailyLimit <= 0 ? null : (int)Math.Ceiling(productsStillNeeded / (decimal)dailyLimit);
        var collectionRows = collections.Select(item => new CatalogueGrowthCollection(
            item.Slug,
            item.DisplayName,
            item.IsPublished,
            item.AssignedProducts,
            item.PublicProducts,
            item.IndexableProducts,
            CollectionPublicationTarget,
            Math.Max(0, CollectionPublicationTarget - item.IndexableProducts),
            !item.IsPublished && item.IndexableProducts >= CollectionPublicationTarget)).ToArray();
        var deferredCandidates = candidateRows.Count(item => item.Disposition == "Deferred until next UTC day");
        var repairCandidates = candidateRows.Count(item => item.Disposition == "Held for repair or review");

        return new CatalogueGrowthPipeline(
            InitialPublicProductTarget,
            publicProducts,
            productsStillNeeded,
            awaiting.Length,
            ready,
            retirement,
            automaticallyRetired,
            deferredCandidates,
            repairCandidates,
            collectionRows.Count(item => item.IsReadyToPublish),
            daysToTarget,
            dailyLimit,
            collectionRows,
            blockers.OrderByDescending(item => item.Value)
                .ThenBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new CatalogueGrowthBlocker(item.Key, Label(item.Key), item.Value))
                .ToArray(),
            candidateRows.OrderBy(item => DispositionOrder(item.Disposition))
                .ThenByDescending(item => item.DraftedUtc)
                .Take(50)
                .ToArray(),
            now);
    }

    private static int DispositionOrder(string disposition) => disposition switch
    {
        "Ready for autonomous approval" => 0,
        "Permanent retirement" => 1,
        "Deferred until next UTC day" => 2,
        _ => 3
    };

    private static string Label(string code) => code switch
    {
        "publication.daily-limit" => "Daily publication limit",
        "published-collection.missing" => "No published collection",
        "duplicate.probable" => "Probable duplicate",
        "affiliate-link.missing" => "Missing affiliate link",
        _ when code.StartsWith("scope.", StringComparison.Ordinal) => "Scope: " + code["scope.".Length..].Replace('-', ' '),
        _ when code.StartsWith("listing.", StringComparison.Ordinal) => "Listing: " + code["listing.".Length..].Replace('-', ' '),
        _ when code.StartsWith("editorial.", StringComparison.Ordinal) => "Editorial: " + code["editorial.".Length..].Replace('-', ' '),
        _ => code.Replace('.', ' ').Replace('-', ' ')
    };

    private static DateTimeOffset StartOfUtcDay(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, 0, 0, 0, TimeSpan.Zero);
}
