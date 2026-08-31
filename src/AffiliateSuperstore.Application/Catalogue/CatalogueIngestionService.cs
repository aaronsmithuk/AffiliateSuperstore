using System.Globalization;
using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record CatalogueIngestionRequest(
    string ShopSlug,
    string? Keywords = null,
    int PageNumber = 1,
    int PageSize = 20);

public sealed record CatalogueIngestionResult(
    Guid JobId,
    IngestionJobStatus Status,
    int ProductsRead,
    int ProductsWritten,
    int ProductsRejected,
    int LinksCreatedOrRefreshed,
    string? Error);

public sealed class CatalogueIngestionService(
    IAffiliateCatalogueSource source,
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    TimeProvider timeProvider,
    ProductQualityAssessmentService qualityAssessmentService)
{
    public async Task<CatalogueIngestionResult> RunAsync(
        CatalogueIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ShopSlug);
        if (request.PageNumber is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(request), "Page number must be between 1 and 100.");
        if (request.PageSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(request), "Page size must be between 1 and 50.");

        var jobId = Guid.CreateVersion7();
        Guid shopId;
        string keywords;
        string trackingId;

        await using (var setup = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            var shop = await setup.Shops.SingleOrDefaultAsync(
                item => item.Slug == request.ShopSlug && item.IsEnabled,
                cancellationToken) ?? throw new InvalidOperationException($"Enabled shop '{request.ShopSlug}' was not found.");

            shopId = shop.Id;
            keywords = string.IsNullOrWhiteSpace(request.Keywords) ? shop.DefaultSearchQuery : request.Keywords.Trim();
            trackingId = shop.TrackingId;
            setup.IngestionJobs.Add(new IngestionJobRecord
            {
                Id = jobId,
                ShopId = shopId,
                Type = IngestionJobType.CatalogueDiscovery,
                Status = IngestionJobStatus.Running,
                QueuedUtc = timeProvider.GetUtcNow(),
                StartedUtc = timeProvider.GetUtcNow(),
                CorrelationId = jobId.ToString("N"),
                Checkpoint = $"page={request.PageNumber};keywords={keywords}"
            });
            await setup.SaveChangesAsync(cancellationToken);
        }

        try
        {
            var page = await source.SearchAsync(
                keywords,
                request.PageNumber,
                request.PageSize,
                cancellationToken);
            var eligible = page.Items.Where(IsMinimallyEligible).ToArray();
            var sourceUrls = eligible
                .Select(product => product.ProductDetailUrl)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var links = await source.GenerateLinksAsync(sourceUrls, trackingId, cancellationToken);
            var linksBySource = links
                .Where(link => !string.IsNullOrWhiteSpace(link.SourceUrl))
                .GroupBy(link => NormaliseUrl(link.SourceUrl), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var linksWritten = await PersistAsync(
                jobId,
                shopId,
                trackingId,
                request,
                page.Items,
                eligible,
                linksBySource,
                cancellationToken);

            return new CatalogueIngestionResult(
                jobId,
                eligible.Length == page.Items.Count ? IngestionJobStatus.Succeeded : IngestionJobStatus.PartiallySucceeded,
                page.Items.Count,
                eligible.Length,
                page.Items.Count - eligible.Length,
                linksWritten,
                null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await MarkFailedAsync(jobId, exception, cancellationToken);
            return new CatalogueIngestionResult(jobId, IngestionJobStatus.Failed, 0, 0, 0, 0, exception.Message);
        }
    }

    private async Task<int> PersistAsync(
        Guid jobId,
        Guid shopId,
        string trackingId,
        CatalogueIngestionRequest request,
        IReadOnlyList<AliExpressProduct> allProducts,
        IReadOnlyList<AliExpressProduct> eligible,
        IReadOnlyDictionary<string, AliExpressPromotionLink> linksBySource,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var ids = eligible.Select(product => product.ProductId).Distinct(StringComparer.Ordinal).ToArray();
        var existingProducts = await context.Products.Where(product => ids.Contains(product.AliExpressProductId)).ToDictionaryAsync(product => product.AliExpressProductId, cancellationToken);
        var existingShopProducts = await context.ShopProducts.Where(item => item.ShopId == shopId && ids.Contains(item.ProductId)).ToDictionaryAsync(item => item.ProductId, cancellationToken);
        var existingLinks = await context.AffiliateLinks
            .Where(link => link.ShopId == shopId && link.ProductId != null && ids.Contains(link.ProductId) && link.Status == AffiliateLinkStatus.Active)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var linksWritten = 0;
        var observationsChanged = 0;
        var correlationId = jobId.ToString("N");

        foreach (var apiProduct in eligible)
        {
            if (!existingProducts.TryGetValue(apiProduct.ProductId, out var product))
            {
                product = new ProductRecord
                {
                    AliExpressProductId = apiProduct.ProductId,
                    FirstSeenUtc = now
                };
                context.Products.Add(product);
                existingProducts.Add(product.AliExpressProductId, product);
            }

            UpdateProduct(product, apiProduct, now);
            var observation = ProductObservationTracker.RecordReturned(
                context,
                product,
                apiProduct,
                "aliexpress.affiliate.product.query",
                correlationId,
                now);
            if (observation.ContentChanged) observationsChanged++;

            if (!existingShopProducts.TryGetValue(apiProduct.ProductId, out var shopProduct))
            {
                shopProduct = new ShopProductRecord
                {
                    ShopId = shopId,
                    ProductId = apiProduct.ProductId,
                    FirstIncludedUtc = now
                };
                context.ShopProducts.Add(shopProduct);
                existingShopProducts.Add(apiProduct.ProductId, shopProduct);
            }

            shopProduct.IsActive = true;
            shopProduct.LastIncludedUtc = now;
            shopProduct.DisabledReason = null;
            var assessment = qualityAssessmentService.Assess(
                apiProduct.Title,
                apiProduct.FirstLevelCategoryName,
                apiProduct.SecondLevelCategoryName);
            shopProduct.AutomatedReviewFlags = assessment.SerializedFlags;
            shopProduct.AutomatedReviewedUtc = now;
            if (assessment.RequiresReview && shopProduct.ReviewStatus is ProductReviewStatus.Pending or ProductReviewStatus.Approved)
            {
                shopProduct.ReviewStatus = ProductReviewStatus.NeedsReview;
            }

            if (apiProduct.ProductDetailUrl is not null &&
                linksBySource.TryGetValue(NormaliseUrl(apiProduct.ProductDetailUrl), out var generated))
            {
                var current = existingLinks.FirstOrDefault(link => link.ProductId == apiProduct.ProductId);
                if (current is not null && string.Equals(current.PromotionUrl, generated.PromotionUrl, StringComparison.Ordinal))
                {
                    current.LastValidatedUtc = now;
                }
                else
                {
                    foreach (var stale in existingLinks.Where(link => link.ProductId == apiProduct.ProductId))
                    {
                        stale.Status = AffiliateLinkStatus.Expired;
                    }

                    context.AffiliateLinks.Add(new AffiliateLinkRecord
                    {
                        Id = Guid.CreateVersion7(),
                        ShopId = shopId,
                        ProductId = apiProduct.ProductId,
                        SourceUrl = generated.SourceUrl,
                        PromotionUrl = generated.PromotionUrl,
                        TrackingId = trackingId,
                        PromotionLinkType = 0,
                        Status = AffiliateLinkStatus.Active,
                        GeneratedUtc = now,
                        LastValidatedUtc = now
                    });
                }

                linksWritten++;
            }
        }

        var job = await context.IngestionJobs.SingleAsync(item => item.Id == jobId, cancellationToken);
        job.ItemsRead = allProducts.Count;
        job.ItemsWritten = eligible.Count;
        job.ItemsRejected = allProducts.Count - eligible.Count;
        job.LinksCreatedOrRefreshed = linksWritten;
        job.Status = job.ItemsRejected == 0 ? IngestionJobStatus.Succeeded : IngestionJobStatus.PartiallySucceeded;
        job.CompletedUtc = now;
        job.Checkpoint = $"page={request.PageNumber};keywords={request.Keywords};changed={observationsChanged};complete=true";
        await context.SaveChangesAsync(cancellationToken);
        return linksWritten;
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

    private static bool IsMinimallyEligible(AliExpressProduct product) =>
        !string.IsNullOrWhiteSpace(product.ProductId) &&
        !string.IsNullOrWhiteSpace(product.Title) &&
        !string.IsNullOrWhiteSpace(product.ProductDetailUrl) &&
        !string.IsNullOrWhiteSpace(product.MainImageUrl) &&
        ParseDecimal(product.TargetSalePrice) is > 0 &&
        string.Equals(product.Currency, "GBP", StringComparison.OrdinalIgnoreCase);

    private static void UpdateProduct(ProductRecord product, AliExpressProduct source, DateTimeOffset now)
    {
        product.Title = source.Title.Trim();
        product.ProductDetailUrl = source.ProductDetailUrl;
        product.MainImageUrl = source.MainImageUrl;
        product.FirstLevelCategoryId = source.FirstLevelCategoryId;
        product.FirstLevelCategoryName = source.FirstLevelCategoryName;
        product.SecondLevelCategoryId = source.SecondLevelCategoryId;
        product.SecondLevelCategoryName = source.SecondLevelCategoryName;
        product.SellerId = source.ShopId;
        product.SellerName = source.ShopName;
        product.SellerUrl = source.ShopUrl;
        product.LastRefreshedUtc = now;
    }

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value?.Trim().TrimEnd('%'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static string NormaliseUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return value.Trim();
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }
}
