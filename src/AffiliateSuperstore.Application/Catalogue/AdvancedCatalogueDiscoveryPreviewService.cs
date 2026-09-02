using System.Globalization;
using System.Diagnostics;
using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record AdvancedDiscoveryPreviewCandidate(
    string ProductId,
    string Title,
    string? ProductDetailUrl,
    decimal? SalePrice,
    string? Currency,
    decimal? CommissionRate,
    decimal? HotProductCommissionRate,
    decimal? EvaluationRate,
    long? RecentSalesVolume,
    string? Category,
    bool IsMinimallyEligible,
    bool IsAlreadyInCatalogue,
    int? ExistingActiveLinkType,
    IReadOnlyList<ProductQualityFlag> QualityFlags)
{
    public string RecommendedAction => !IsMinimallyEligible
        ? "Reject: incomplete or non-GBP result"
        : IsAlreadyInCatalogue
            ? "Already in catalogue"
            : QualityFlags.Count > 0
                ? "Hold for flagged review"
                : "New review candidate";
}

public sealed record AdvancedDiscoveryPreviewResult(
    string ShopSlug,
    CatalogueDiscoverySource Source,
    string Keywords,
    string? SeedProductId,
    int ProductsRead,
    int MinimallyEligible,
    int AlreadyInCatalogue,
    int QualityClearNewCandidates,
    IReadOnlyList<AdvancedDiscoveryPreviewCandidate> Candidates);

public sealed class AdvancedCatalogueDiscoveryPreviewService(
    IAffiliateCatalogueSource source,
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    ProductQualityAssessmentService qualityAssessmentService)
{
    public async Task<AdvancedDiscoveryPreviewResult> PreviewAsync(
        string shopSlug,
        CatalogueDiscoverySource discoverySource,
        string keywords,
        string? seedProductId = null,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(keywords);
        if (discoverySource is not CatalogueDiscoverySource.HotProductQuery and not CatalogueDiscoverySource.SmartMatch)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discoverySource), discoverySource, "Preview supports only Advanced discovery sources.");
        }
        if (pageSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(pageSize));

        var normalizedShopSlug = shopSlug.Trim();
        var normalizedKeywords = keywords.Trim();
        var page = discoverySource switch
        {
            CatalogueDiscoverySource.HotProductQuery => await source.SearchHotProductsAsync(
                normalizedKeywords, 1, pageSize, cancellationToken),
            CatalogueDiscoverySource.SmartMatch => await source.SmartMatchAsync(
                NullIfWhiteSpace(seedProductId), normalizedKeywords, 1, cancellationToken),
            _ => throw new UnreachableException()
        };

        var ids = page.Items
            .Select(item => item.ProductId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var shop = await context.Shops.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Slug == normalizedShopSlug && item.IsEnabled, cancellationToken)
            ?? throw new InvalidOperationException($"Enabled shop '{normalizedShopSlug}' was not found.");
        var existingIds = await context.Products.AsNoTracking()
            .Where(item => ids.Contains(item.AliExpressProductId))
            .Select(item => item.AliExpressProductId)
            .ToHashSetAsync(StringComparer.Ordinal, cancellationToken);
        var activeLinkTypes = await context.AffiliateLinks.AsNoTracking()
            .Where(item => item.ShopId == shop.Id &&
                           item.ProductId != null &&
                           ids.Contains(item.ProductId) &&
                           item.Status == AffiliateLinkStatus.Active)
            .GroupBy(item => item.ProductId!)
            .Select(group => new { ProductId = group.Key, LinkType = group.Max(item => item.PromotionLinkType) })
            .ToDictionaryAsync(item => item.ProductId, item => item.LinkType, StringComparer.Ordinal, cancellationToken);

        var candidates = page.Items.Select(item =>
        {
            var minimallyEligible = CatalogueProductEligibility.IsMinimallyEligible(item);
            var quality = string.IsNullOrWhiteSpace(item.Title)
                ? new ProductQualityAssessment(
                    [new ProductQualityFlag("source.missing-title", "AliExpress did not return a product title.")])
                : qualityAssessmentService.Assess(
                    item.Title,
                    item.FirstLevelCategoryName,
                    item.SecondLevelCategoryName,
                    requirePlushEvidence: true);
            return new AdvancedDiscoveryPreviewCandidate(
                item.ProductId,
                item.Title,
                item.ProductDetailUrl,
                ParseDecimal(item.TargetSalePrice),
                item.Currency,
                ParseRate(item.CommissionRate),
                ParseRate(item.HotProductCommissionRate),
                ParseRate(item.EvaluationRate),
                item.RecentSalesVolume,
                item.SecondLevelCategoryName ?? item.FirstLevelCategoryName,
                minimallyEligible,
                existingIds.Contains(item.ProductId),
                activeLinkTypes.GetValueOrDefault(item.ProductId),
                quality.Flags);
        }).ToArray();

        return new AdvancedDiscoveryPreviewResult(
            normalizedShopSlug,
            discoverySource,
            normalizedKeywords,
            NullIfWhiteSpace(seedProductId),
            candidates.Length,
            candidates.Count(item => item.IsMinimallyEligible),
            candidates.Count(item => item.IsAlreadyInCatalogue),
            candidates.Count(item => item.IsMinimallyEligible && !item.IsAlreadyInCatalogue && item.QualityFlags.Count == 0),
            candidates);
    }

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static decimal? ParseRate(string? value) =>
        decimal.TryParse(value?.Trim().TrimEnd('%'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed / 100m
            : null;

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class CatalogueProductEligibility
{
    public static bool IsMinimallyEligible(AliExpressProduct product) =>
        !string.IsNullOrWhiteSpace(product.ProductId) &&
        !string.IsNullOrWhiteSpace(product.Title) &&
        !string.IsNullOrWhiteSpace(product.ProductDetailUrl) &&
        !string.IsNullOrWhiteSpace(product.MainImageUrl) &&
        ParseDecimal(product.TargetSalePrice) is > 0 &&
        string.Equals(product.Currency, "GBP", StringComparison.OrdinalIgnoreCase);

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}
