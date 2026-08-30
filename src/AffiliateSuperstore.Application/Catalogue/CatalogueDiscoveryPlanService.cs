using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence.Entities;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record CatalogueDiscoveryPlanResult(
    string ShopSlug,
    IngestionJobStatus Status,
    int RequestsPlanned,
    int RequestsCompleted,
    int ProductsRead,
    int ProductsWritten,
    int ProductsRejected,
    int LinksCreatedOrRefreshed,
    IReadOnlyList<CatalogueIngestionResult> Runs,
    string? Error);

public sealed class CatalogueDiscoveryPlanService(
    CatalogueIngestionService ingestionService,
    AffiliateSuperstoreOptions superstoreOptions)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<CatalogueDiscoveryPlanResult> RunAsync(
        string shopSlug,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        var shop = superstoreOptions.Shops.SingleOrDefault(item =>
            item.IsEnabled && string.Equals(item.Slug, shopSlug, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Enabled shop '{shopSlug}' was not found in configuration.");
        var plan = CatalogueDiscoveryPlanner.Build(shop, pageSize);
        if (!await Gate.WaitAsync(0, cancellationToken))
        {
            return new CatalogueDiscoveryPlanResult(
                shop.Slug,
                IngestionJobStatus.Running,
                plan.Count,
                0,
                0,
                0,
                0,
                0,
                [],
                "A catalogue discovery plan is already running in this application instance.");
        }

        try
        {
            var runs = new List<CatalogueIngestionResult>(plan.Count);
            foreach (var request in plan)
            {
                var result = await ingestionService.RunAsync(request, cancellationToken);
                runs.Add(result);
                if (result.Status == IngestionJobStatus.Failed) break;
            }

            var status = runs.Any(run => run.Status == IngestionJobStatus.Failed)
                ? IngestionJobStatus.Failed
                : runs.Any(run => run.Status == IngestionJobStatus.PartiallySucceeded)
                    ? IngestionJobStatus.PartiallySucceeded
                    : IngestionJobStatus.Succeeded;
            return new CatalogueDiscoveryPlanResult(
                shop.Slug,
                status,
                plan.Count,
                runs.Count,
                runs.Sum(run => run.ProductsRead),
                runs.Sum(run => run.ProductsWritten),
                runs.Sum(run => run.ProductsRejected),
                runs.Sum(run => run.LinksCreatedOrRefreshed),
                runs,
                runs.LastOrDefault(run => run.Status == IngestionJobStatus.Failed)?.Error);
        }
        finally
        {
            Gate.Release();
        }
    }
}
