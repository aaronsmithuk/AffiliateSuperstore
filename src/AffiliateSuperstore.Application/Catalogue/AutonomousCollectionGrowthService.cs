using AffiliateSuperstore.Persistence.Entities;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record AutomaticCollectionPublicationItem(
    Guid CollectionId,
    string Slug,
    string DisplayName,
    int IndexableProducts,
    int RequiredProducts,
    bool Published,
    string Message);

public sealed record AutomaticCollectionPublicationResult(
    string ShopSlug,
    int CollectionsConsidered,
    int CollectionsPublished,
    IReadOnlyList<AutomaticCollectionPublicationItem> Items,
    string Message);

public sealed class AutonomousCollectionPublicationService(
    CatalogueCollectionService collectionService,
    AutonomousCataloguePolicyService policyService,
    AutonomousCatalogueOptions options)
{
    public async Task<AutomaticCollectionPublicationResult> RunAsync(
        string shopSlug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        var policy = await policyService.GetAsync(shopSlug, cancellationToken);
        if (policy is null)
        {
            return Empty(shopSlug, "No autonomous catalogue policy exists for this shop.");
        }
        if (!options.Enabled ||
            !options.AutomaticPublishingEnabled ||
            !options.AutomaticCollectionPublishingEnabled ||
            policy.Mode != AutonomousCatalogueMode.Automatic)
        {
            return Empty(shopSlug, "Automatic collection publication is not armed for this shop.");
        }

        var minimum = Math.Clamp(
            options.MinimumAutomaticCollectionProducts,
            CatalogueCollectionService.MinimumIndexingTarget,
            CatalogueCollectionService.MaximumIndexingTarget);
        var collections = await collectionService.GetCollectionsAsync(shopSlug, cancellationToken);
        var ready = collections
            .Where(item => !item.IsPublished)
            .Select(item => new
            {
                Summary = item,
                RequiredProducts = Math.Max(minimum, item.MinimumProductsForIndexing)
            })
            .Where(item => item.Summary.IndexableProducts >= item.RequiredProducts)
            .OrderBy(item => item.Summary.DisplayOrder)
            .ThenBy(item => item.Summary.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (ready.Length == 0)
        {
            return Empty(shopSlug, $"No draft collection has reached the automatic threshold of at least {minimum} indexable products.");
        }

        var results = new List<AutomaticCollectionPublicationItem>(ready.Length);
        foreach (var candidate in ready)
        {
            var reason = $"Automatically published after {candidate.Summary.IndexableProducts} products passed every product and collection indexability gate; required {candidate.RequiredProducts}.";
            var result = await collectionService.SetPublicationAsync(
                shopSlug,
                candidate.Summary.Id,
                true,
                actor: "autonomous collection policy",
                mode: CollectionPublicationMode.Automatic,
                reason: reason,
                requiredMinimumProducts: candidate.RequiredProducts,
                cancellationToken: cancellationToken);
            results.Add(new AutomaticCollectionPublicationItem(
                candidate.Summary.Id,
                candidate.Summary.Slug,
                candidate.Summary.DisplayName,
                candidate.Summary.IndexableProducts,
                candidate.RequiredProducts,
                result.Succeeded,
                result.Message));
        }

        var published = results.Count(item => item.Published);
        return new AutomaticCollectionPublicationResult(
            shopSlug,
            results.Count,
            published,
            results,
            published == results.Count
                ? $"Automatically published {published} collection{(published == 1 ? string.Empty : "s")} after all final gates passed."
                : $"Automatically published {published} of {results.Count} ready collections; failed final gates remain private.");
    }

    private static AutomaticCollectionPublicationResult Empty(string shopSlug, string message) =>
        new(shopSlug, 0, 0, [], message);
}

public sealed record AutonomousCollectionGrowthResult(
    string ShopSlug,
    string? TargetCollection,
    int ProductsRead,
    int ProductsWritten,
    int ProductsAssigned,
    int ExistingCandidatesAssigned,
    int CollectionsPublished,
    bool Succeeded,
    string Message);

public sealed class AutonomousCollectionGrowthService(
    CatalogueCollectionService collectionService,
    CatalogueCollectionDiscoveryService discoveryService,
    AutonomousCollectionPublicationService publicationService,
    AutonomousCataloguePolicyService policyService,
    AutonomousCatalogueOptions options)
{
    private static readonly HashSet<string> PermittedPreparationIssues = new(StringComparer.Ordinal)
    {
        "Awaiting editorial approval",
        "Editorial title needs work",
        "Editorial description needs work"
    };

    public async Task<AutonomousCollectionGrowthResult> RunAsync(
        string shopSlug,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        var policy = await policyService.GetAsync(shopSlug, cancellationToken);
        if (policy is null ||
            !options.Enabled ||
            !options.AutomaticCollectionGrowthEnabled ||
            policy.Mode != AutonomousCatalogueMode.Automatic)
        {
            return Empty(shopSlug, "Autonomous collection growth is not armed for this shop.");
        }

        var assignmentLimit = Math.Clamp(
            options.MaximumAutomaticCollectionAssignmentsPerRun,
            1,
            CatalogueCollectionService.MaximumBatchAssignments);
        var minimum = Math.Clamp(
            options.MinimumAutomaticCollectionProducts,
            CatalogueCollectionService.MinimumIndexingTarget,
            CatalogueCollectionService.MaximumIndexingTarget);
        var collections = await collectionService.GetCollectionsAsync(shopSlug, cancellationToken);
        var underfilled = collections
            .Where(item => item.IndexableProducts < Math.Max(minimum, item.MinimumProductsForIndexing))
            .OrderBy(item => item.IsPublished ? 1 : 0)
            .ThenByDescending(item => item.IndexableProducts)
            .ThenBy(item => item.DisplayOrder)
            .ToArray();
        if (underfilled.Length == 0)
        {
            var publication = await publicationService.RunAsync(shopSlug, cancellationToken);
            return new AutonomousCollectionGrowthResult(
                shopSlug,
                null,
                0,
                0,
                0,
                0,
                publication.CollectionsPublished,
                true,
                "Every collection has reached its automatic indexable-product target.");
        }

        var target = underfilled[0];
        var discovery = await discoveryService.RunAsync(
            shopSlug,
            target.Id,
            pageSize,
            "autonomous collection growth",
            assignmentLimit,
            cancellationToken);
        var remaining = Math.Max(0, assignmentLimit - discovery.ProductsAssigned);
        var assignedExisting = 0;
        if (remaining > 0)
        {
            foreach (var collection in underfilled)
            {
                if (remaining == 0) break;
                var candidates = await collectionService.GetProductCandidatesAsync(
                    shopSlug,
                    collection.Id,
                    filter: CollectionCandidateFilter.Suggested,
                    maximumResults: 250,
                    cancellationToken: cancellationToken);
                var selected = candidates
                    .Where(item => !item.IsAssigned)
                    .Where(item => item.ReviewStatus != ProductReviewStatus.Rejected)
                    .Where(item => item.ReadinessIssues.All(PermittedPreparationIssues.Contains))
                    .OrderByDescending(item => item.IsIndexable)
                    .ThenByDescending(item => item.CollectionMatchScore)
                    .ThenByDescending(item => item.RecentSalesVolume)
                    .ThenBy(item => item.ProductId, StringComparer.Ordinal)
                    .Take(remaining)
                    .Select(item => item.ProductId)
                    .ToArray();
                if (selected.Length == 0) continue;

                var result = await collectionService.AddMembershipsAsync(
                    shopSlug,
                    collection.Id,
                    selected,
                    "autonomous collection growth",
                    cancellationToken);
                if (!result.Succeeded) continue;
                assignedExisting += selected.Length;
                remaining -= selected.Length;
            }
        }

        var publicationResult = await publicationService.RunAsync(shopSlug, cancellationToken);
        var succeeded = discovery.Succeeded;
        return new AutonomousCollectionGrowthResult(
            shopSlug,
            target.DisplayName,
            discovery.ProductsRead,
            discovery.ProductsWritten,
            discovery.ProductsAssigned,
            assignedExisting,
            publicationResult.CollectionsPublished,
            succeeded,
            $"Targeted {target.DisplayName}: discovered {discovery.ProductsRead}, assigned {discovery.ProductsAssigned + assignedExisting}, and automatically published {publicationResult.CollectionsPublished} collections. {discovery.Message}");
    }

    private static AutonomousCollectionGrowthResult Empty(string shopSlug, string message) =>
        new(shopSlug, null, 0, 0, 0, 0, 0, true, message);
}
