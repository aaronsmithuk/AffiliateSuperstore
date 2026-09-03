using System.Text.Json;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record CollectionDiscoveryRun(
    string Query,
    IngestionJobStatus Status,
    int ProductsRead,
    int ProductsWritten,
    string? Error);

public sealed record CollectionDiscoveryResult(
    Guid CollectionId,
    string CollectionName,
    int QueriesPlanned,
    int QueriesCompleted,
    int ProductsRead,
    int ProductsWritten,
    int ProductsAssigned,
    IReadOnlyList<CollectionDiscoveryRun> Runs,
    string Message,
    bool Succeeded);

public sealed class CatalogueCollectionDiscoveryService(
    CatalogueIngestionService ingestionService,
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    TimeProvider timeProvider)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<CollectionDiscoveryResult> RunAsync(
        string shopSlug,
        Guid collectionId,
        int pageSize,
        string actor,
        int maximumAssignments = CatalogueCollectionService.MaximumBatchAssignments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        pageSize = Math.Clamp(pageSize, 1, 50);

        CollectionSeed seed;
        await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            seed = await context.Collections.AsNoTracking()
                .Where(item => item.Id == collectionId && item.Shop.Slug == shopSlug && item.Shop.IsEnabled)
                .Select(item => new CollectionSeed(
                    item.Id,
                    item.DisplayName,
                    item.ShortDescription,
                    item.DiscoveryQueriesJson))
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException($"Collection '{collectionId}' was not found for shop '{shopSlug}'.");
        }

        var queries = ReadQueries(seed.DiscoveryQueriesJson);
        if (queries.Count == 0)
        {
            return Empty(seed, "Add at least one generic discovery query before running collection discovery.");
        }

        if (!await Gate.WaitAsync(0, cancellationToken))
        {
            return Empty(seed, "Another collection discovery run is already active in this application instance.");
        }

        try
        {
            var runs = new List<CollectionDiscoveryRun>(queries.Count);
            var productIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var query in queries)
            {
                var result = await ingestionService.RunAsync(
                    new CatalogueIngestionRequest(shopSlug, query, 1, pageSize),
                    cancellationToken);
                runs.Add(new CollectionDiscoveryRun(
                    query,
                    result.Status,
                    result.ProductsRead,
                    result.ProductsWritten,
                    result.Error));
                productIds.UnionWith(result.ProductIds);
                if (result.Status == IngestionJobStatus.Failed) break;
            }

            var assigned = await AssignProductsAsync(
                shopSlug,
                seed,
                productIds,
                actor,
                Math.Clamp(maximumAssignments, 1, CatalogueCollectionService.MaximumBatchAssignments),
                cancellationToken);
            var failed = runs.Any(item => item.Status == IngestionJobStatus.Failed);
            return new CollectionDiscoveryResult(
                seed.Id,
                seed.DisplayName,
                queries.Count,
                runs.Count,
                runs.Sum(item => item.ProductsRead),
                runs.Sum(item => item.ProductsWritten),
                assigned,
                runs,
                failed
                    ? $"Discovery stopped after an API failure. {assigned} newly discovered products were still assigned as non-public catalogue candidates."
                    : $"Completed {runs.Count} searches and assigned {assigned} new catalogue candidates. Review and approve products before publishing them.",
                !failed);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<int> AssignProductsAsync(
        string shopSlug,
        CollectionSeed seed,
        IReadOnlySet<string> productIds,
        string actor,
        int maximumAssignments,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0) return 0;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var collection = await context.Collections
            .Include(item => item.Products)
            .SingleAsync(item => item.Id == seed.Id && item.Shop.Slug == shopSlug, cancellationToken);
        var queries = ReadQueries(seed.DiscoveryQueriesJson);
        var assignableRows = await context.ShopProducts.AsNoTracking()
            .Where(item =>
                item.ShopId == collection.ShopId &&
                item.IsActive &&
                item.Product.IsEligible &&
                productIds.Contains(item.ProductId))
            .Select(item => new
            {
                item.ProductId,
                SourceTitle = item.Product.Title,
                item.EditorialTitle,
                SourceCategory = item.Product.SecondLevelCategoryName,
                NormalizedIdentityTitle = item.Product.IdentityProfile == null
                    ? null
                    : item.Product.IdentityProfile.NormalizedTitle,
                RecentSalesVolume = item.Product.Snapshots.Max(snapshot => snapshot.RecentSalesVolume)
            })
            .ToListAsync(cancellationToken);
        var assignableIds = assignableRows
            .Select(item => new
            {
                item.ProductId,
                item.RecentSalesVolume,
                Match = CollectionCandidateMatcher.Assess(
                    seed.DisplayName,
                    seed.ShortDescription,
                    queries,
                    item.SourceTitle,
                    item.EditorialTitle,
                    item.SourceCategory,
                    item.NormalizedIdentityTitle)
            })
            .Where(item => item.Match.IsSuggested)
            .OrderByDescending(item => item.Match.Score)
            .ThenByDescending(item => item.RecentSalesVolume)
            .ThenBy(item => item.ProductId, StringComparer.Ordinal)
            .Select(item => item.ProductId)
            .Take(maximumAssignments)
            .ToArray();
        var existing = collection.Products.Select(item => item.ProductId).ToHashSet(StringComparer.Ordinal);
        var nextOrder = collection.Products.Count == 0 ? 10 : collection.Products.Max(item => item.DisplayOrder) + 10;
        var now = timeProvider.GetUtcNow();
        var added = 0;
        foreach (var productId in assignableIds.Where(item => !existing.Contains(item)))
        {
            collection.Products.Add(new CollectionProductRecord
            {
                CollectionId = collection.Id,
                ProductId = productId,
                DisplayOrder = nextOrder,
                AssignedUtc = now,
                AssignedBy = string.IsNullOrWhiteSpace(actor) ? "administrator" : actor.Trim()
            });
            nextOrder += 10;
            added++;
        }

        collection.UpdatedUtc = now;
        await context.SaveChangesAsync(cancellationToken);
        return added;
    }

    private static IReadOnlyList<string> ReadQueries(string json)
    {
        try
        {
            return (JsonSerializer.Deserialize<string[]>(json) ?? [])
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(CatalogueCollectionService.MaximumDiscoveryQueries)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static CollectionDiscoveryResult Empty(CollectionSeed seed, string message) =>
        new(seed.Id, seed.DisplayName, 0, 0, 0, 0, 0, [], message, false);

    private sealed record CollectionSeed(
        Guid Id,
        string DisplayName,
        string ShortDescription,
        string DiscoveryQueriesJson);
}
