using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record CatalogueProductEnrichmentResult(
    Guid JobId,
    IngestionJobStatus Status,
    int ProductsSelected,
    int ProductsEnriched,
    int ProductsMissing,
    int MediaItemsStored,
    string? Error,
    int ProductsChanged = 0,
    int ProductsSuspectedUnavailable = 0,
    int ProductsConfirmedUnavailable = 0,
    int ProductsRestored = 0);

public sealed class CatalogueProductEnrichmentService(
    IAffiliateProductDetailSource source,
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    TimeProvider timeProvider,
    ProductQualityAssessmentService qualityAssessmentService)
{
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);
    public static readonly TimeSpan DefaultFreshness = TimeSpan.FromHours(24);
    public static readonly TimeSpan SuspectedUnavailableRetry = TimeSpan.FromHours(6);
    public static readonly TimeSpan ConfirmedUnavailableRetry = TimeSpan.FromDays(7);
    private const int DetailBatchSize = 50;

    public async Task<CatalogueProductEnrichmentResult> RunAsync(
        string shopSlug,
        bool force = false,
        int maximumProducts = 200,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        if (maximumProducts is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(maximumProducts));

        await RefreshGate.WaitAsync(cancellationToken);
        try
        {
            return await RunCoreAsync(shopSlug.Trim(), force, maximumProducts, cancellationToken);
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    private async Task<CatalogueProductEnrichmentResult> RunCoreAsync(
        string shopSlug,
        bool force,
        int maximumProducts,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var staleBefore = now - DefaultFreshness;
        var suspectedBefore = now - SuspectedUnavailableRetry;
        var unavailableBefore = now - ConfirmedUnavailableRetry;
        var jobId = Guid.CreateVersion7();
        Guid shopId;
        string[] productIds;

        await using (var setup = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            var shop = await setup.Shops.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Slug == shopSlug && item.IsEnabled, cancellationToken)
                ?? throw new InvalidOperationException($"Enabled shop '{shopSlug}' was not found.");
            shopId = shop.Id;
            productIds = await setup.ShopProducts.AsNoTracking()
                .Where(item => item.ShopId == shopId &&
                               item.IsActive &&
                               item.ReviewStatus == ProductReviewStatus.Approved &&
                               (item.Product.IsEligible || item.Product.AvailabilityState == ProductAvailabilityState.Unavailable) &&
                               item.Product.AffiliateLinks.Any(link => link.ShopId == shopId && link.Status == AffiliateLinkStatus.Active) &&
                               (force ||
                                (item.Product.AvailabilityState == ProductAvailabilityState.Available &&
                                 (item.Product.LastDetailRefreshedUtc == null || item.Product.LastDetailRefreshedUtc < staleBefore)) ||
                                (item.Product.AvailabilityState == ProductAvailabilityState.SuspectedUnavailable &&
                                 (item.Product.LastCheckedUtc == null || item.Product.LastCheckedUtc < suspectedBefore)) ||
                                (item.Product.AvailabilityState == ProductAvailabilityState.Unavailable &&
                                 (item.Product.LastCheckedUtc == null || item.Product.LastCheckedUtc < unavailableBefore))))
                .OrderBy(item => item.Product.LastDetailRefreshedUtc)
                .ThenByDescending(item => item.IsFeatured)
                .ThenBy(item => item.DisplayOrder)
                .Select(item => item.ProductId)
                .Take(maximumProducts)
                .ToArrayAsync(cancellationToken);

            setup.IngestionJobs.Add(new IngestionJobRecord
            {
                Id = jobId,
                ShopId = shopId,
                Type = IngestionJobType.ProductRefresh,
                Status = IngestionJobStatus.Running,
                QueuedUtc = now,
                StartedUtc = now,
                CorrelationId = jobId.ToString("N"),
                Checkpoint = $"shop={shopSlug};force={force};selected={productIds.Length}"
            });
            await setup.SaveChangesAsync(cancellationToken);
        }

        if (productIds.Length == 0)
        {
            await CompleteEmptyAsync(jobId, now, cancellationToken);
            return new(jobId, IngestionJobStatus.Succeeded, 0, 0, 0, 0, null);
        }

        try
        {
            var details = new List<AliExpressProduct>(productIds.Length);
            foreach (var batch in productIds.Chunk(DetailBatchSize))
            {
                details.AddRange(await source.GetDetailsAsync(batch, cancellationToken));
            }

            return await PersistAsync(jobId, shopId, productIds, details, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await MarkFailedAsync(jobId, exception, cancellationToken);
            return new(jobId, IngestionJobStatus.Failed, productIds.Length, 0, productIds.Length, 0, exception.Message);
        }
    }

    private async Task<CatalogueProductEnrichmentResult> PersistAsync(
        Guid jobId,
        Guid shopId,
        IReadOnlyCollection<string> selectedIds,
        IReadOnlyCollection<AliExpressProduct> details,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var selected = selectedIds.ToArray();
        var products = await context.Products
            .Where(item => selected.Contains(item.AliExpressProductId))
            .ToDictionaryAsync(item => item.AliExpressProductId, cancellationToken);
        var shopProducts = await context.ShopProducts
            .Where(item => item.ShopId == shopId && selected.Contains(item.ProductId))
            .ToDictionaryAsync(item => item.ProductId, cancellationToken);
        var currentMedia = await context.ProductMedia
            .Where(item => selected.Contains(item.ProductId))
            .ToListAsync(cancellationToken);
        var returned = details
            .Where(item => !string.IsNullOrWhiteSpace(item.ProductId))
            .GroupBy(item => item.ProductId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var enriched = 0;
        var mediaStored = 0;
        var changed = 0;
        var suspectedUnavailable = 0;
        var confirmedUnavailable = 0;
        var restored = 0;
        var correlationId = jobId.ToString("N");

        foreach (var productId in selected)
        {
            if (!products.TryGetValue(productId, out var product) || !returned.TryGetValue(productId, out var detail)) continue;

            UpdateProduct(product, detail, now);
            var observation = ProductObservationTracker.RecordReturned(
                context,
                product,
                detail,
                "aliexpress.affiliate.productdetail.get",
                correlationId,
                now);
            if (observation.ContentChanged) changed++;
            if (observation.Restored) restored++;

            var existingMedia = currentMedia.Where(item => item.ProductId == productId).ToArray();
            if (observation.ContentChanged || existingMedia.Length == 0)
            {
                context.ProductMedia.RemoveRange(existingMedia);
                var position = 0;
                foreach (var imageUrl in BuildImageUrls(detail))
                {
                    context.ProductMedia.Add(new ProductMediaRecord
                    {
                        Id = Guid.CreateVersion7(),
                        ProductId = productId,
                        Type = ProductMediaType.Image,
                        Url = imageUrl,
                        Position = position++,
                        RefreshedUtc = now
                    });
                    mediaStored++;
                }
                if (IsSafeRemoteMediaUrl(detail.VideoUrl))
                {
                    context.ProductMedia.Add(new ProductMediaRecord
                    {
                        Id = Guid.CreateVersion7(),
                        ProductId = productId,
                        Type = ProductMediaType.Video,
                        Url = detail.VideoUrl!,
                        Position = 0,
                        RefreshedUtc = now
                    });
                    mediaStored++;
                }
            }

            if (observation.ContentChanged && shopProducts.TryGetValue(productId, out var shopProduct))
            {
                var assessment = qualityAssessmentService.AssessForPublication(
                    product.Title,
                    shopProduct.EditorialTitle,
                    product.FirstLevelCategoryName,
                    product.SecondLevelCategoryName);
                shopProduct.AutomatedReviewFlags = assessment.SerializedFlags;
                shopProduct.AutomatedReviewedUtc = now;
                if (assessment.RequiresReview && shopProduct.ReviewStatus == ProductReviewStatus.Approved)
                {
                    shopProduct.ReviewStatus = ProductReviewStatus.NeedsReview;
                }
            }
            enriched++;
        }

        foreach (var productId in selected.Where(productId => !returned.ContainsKey(productId)))
        {
            if (!products.TryGetValue(productId, out var product)) continue;
            product.LastDetailRefreshedUtc = now;
            var observation = ProductObservationTracker.RecordMissingDirect(
                context,
                product,
                "aliexpress.affiliate.productdetail.get",
                correlationId,
                now);
            if (observation.SuspectedUnavailable) suspectedUnavailable++;
            if (observation.ConfirmedUnavailable) confirmedUnavailable++;
        }

        var missing = selected.Length - enriched;
        var status = missing == 0 ? IngestionJobStatus.Succeeded : IngestionJobStatus.PartiallySucceeded;
        var job = await context.IngestionJobs.SingleAsync(item => item.Id == jobId, cancellationToken);
        job.ItemsRead = selected.Length;
        job.ItemsWritten = enriched;
        job.ItemsRejected = missing;
        job.Status = status;
        job.CompletedUtc = now;
        job.Checkpoint = $"enriched={enriched};missing={missing};changed={changed};suspected={suspectedUnavailable};unavailable={confirmedUnavailable};restored={restored};media={mediaStored};complete=true";
        await context.SaveChangesAsync(cancellationToken);
        return new(jobId, status, selected.Length, enriched, missing, mediaStored, null,
            changed, suspectedUnavailable, confirmedUnavailable, restored);
    }

    private static void UpdateProduct(ProductRecord product, AliExpressProduct detail, DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(detail.Title)) product.Title = detail.Title.Trim();
        if (!string.IsNullOrWhiteSpace(detail.ProductDetailUrl)) product.ProductDetailUrl = detail.ProductDetailUrl;
        if (IsSafeRemoteMediaUrl(detail.MainImageUrl)) product.MainImageUrl = detail.MainImageUrl;
        product.FirstLevelCategoryId = detail.FirstLevelCategoryId ?? product.FirstLevelCategoryId;
        product.FirstLevelCategoryName = detail.FirstLevelCategoryName ?? product.FirstLevelCategoryName;
        product.SecondLevelCategoryId = detail.SecondLevelCategoryId ?? product.SecondLevelCategoryId;
        product.SecondLevelCategoryName = detail.SecondLevelCategoryName ?? product.SecondLevelCategoryName;
        product.SellerId = detail.ShopId ?? product.SellerId;
        product.SellerName = detail.ShopName ?? product.SellerName;
        product.SellerUrl = detail.ShopUrl ?? product.SellerUrl;
        product.SkuId = detail.SkuId ?? product.SkuId;
        product.EanCode = detail.EanCode ?? product.EanCode;
        product.LastRefreshedUtc = now;
        product.LastDetailRefreshedUtc = now;
    }

    private static IReadOnlyList<string> BuildImageUrls(AliExpressProduct detail) =>
        new[] { detail.MainImageUrl }
            .Concat(detail.SmallImageUrls ?? [])
            .Where(IsSafeRemoteMediaUrl)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool IsSafeRemoteMediaUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private async Task CompleteEmptyAsync(Guid jobId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var job = await context.IngestionJobs.SingleAsync(item => item.Id == jobId, cancellationToken);
        job.Status = IngestionJobStatus.Succeeded;
        job.CompletedUtc = now;
        job.Checkpoint += ";complete=true";
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkFailedAsync(Guid jobId, Exception exception, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var job = await context.IngestionJobs.SingleAsync(item => item.Id == jobId, cancellationToken);
        job.Status = IngestionJobStatus.Failed;
        job.CompletedUtc = timeProvider.GetUtcNow();
        job.ErrorSummary = $"{exception.GetType().Name}: {exception.Message}";
        await context.SaveChangesAsync(cancellationToken);
    }
}
