using System.Xml.Linq;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Web.Pages;

public sealed class SitemapModel(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    CatalogueSeoPolicy seoPolicy,
    CatalogueSeoOptions seoOptions,
    AffiliateSuperstoreOptions superstoreOptions) : PageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await context.ShopProducts.AsNoTracking()
            .Where(item =>
                item.Shop.IsEnabled &&
                item.IsActive &&
                item.ReviewStatus == ProductReviewStatus.Approved &&
                item.Product.IsEligible &&
                item.Product.AffiliateLinks.Any(link =>
                    link.ShopId == item.ShopId && link.Status == AffiliateLinkStatus.Active))
            .Select(item => new SitemapCandidate(
                item.Shop.Slug,
                item.ProductId,
                item.EditorialTitle,
                item.EditorialDescription,
                item.Product.MainImageUrl,
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.SalePrice).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc).Select(snapshot => snapshot.FetchedUtc).FirstOrDefault(),
                item.Product.LastRefreshedUtc))
            .ToListAsync(cancellationToken);

        var indexable = seoOptions.IndexingEnabled
            ? candidates.Where(candidate => seoPolicy.IsProductIndexable(
            candidate.EditorialTitle,
            candidate.EditorialDescription,
            candidate.ImageUrl,
            candidate.Price,
            candidate.LastCheckedUtc)).ToArray()
            : [];
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urls = new List<XElement>();
        foreach (var shop in indexable.GroupBy(candidate => candidate.ShopSlug))
        {
            if (CatalogueSeoPolicy.IsShopIndexable(shop.Count()))
            {
                urls.Add(CreateUrlElement(ns, superstoreOptions.BuildPublicUrl($"/{shop.Key}"), shop.Max(candidate => candidate.LastRefreshedUtc)));
            }
            urls.AddRange(shop.Select(candidate => CreateUrlElement(
                ns,
                superstoreOptions.BuildPublicUrl($"/{candidate.ShopSlug}/product/{candidate.ProductId}"),
                candidate.LastRefreshedUtc)));
        }

        if (seoOptions.IndexingEnabled)
        {
            var collections = await context.Collections.AsNoTracking()
                .Where(item => item.IsPublished && item.Shop.IsEnabled)
                .Select(item => new SitemapCollection(
                    item.Id,
                    item.Shop.Slug,
                    item.Slug,
                    item.MinimumProductsForIndexing,
                    item.UpdatedUtc))
                .ToListAsync(cancellationToken);
            if (collections.Count > 0)
            {
                var collectionIds = collections.Select(item => item.Id).ToArray();
                var collectionProducts = await (
                    from membership in context.CollectionProducts.AsNoTracking()
                    join collection in context.Collections.AsNoTracking() on membership.CollectionId equals collection.Id
                    join shopProduct in context.ShopProducts.AsNoTracking()
                        on new { collection.ShopId, membership.ProductId }
                        equals new { shopProduct.ShopId, shopProduct.ProductId }
                    where collectionIds.Contains(collection.Id) &&
                        shopProduct.IsActive &&
                        shopProduct.ReviewStatus == ProductReviewStatus.Approved &&
                        shopProduct.Product.IsEligible &&
                        shopProduct.Product.AffiliateLinks.Any(link =>
                            link.ShopId == shopProduct.ShopId && link.Status == AffiliateLinkStatus.Active)
                    select new SitemapCollectionProduct(
                        collection.Id,
                        shopProduct.EditorialTitle,
                        shopProduct.EditorialDescription,
                        shopProduct.Product.MainImageUrl,
                        shopProduct.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc)
                            .Select(snapshot => snapshot.SalePrice).FirstOrDefault(),
                        shopProduct.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc)
                            .Select(snapshot => snapshot.FetchedUtc).FirstOrDefault()))
                    .ToListAsync(cancellationToken);

                urls.AddRange(collections
                    .Where(collection => collectionProducts.Count(product =>
                        product.CollectionId == collection.Id &&
                        seoPolicy.IsProductIndexable(
                            product.EditorialTitle,
                            product.EditorialDescription,
                            product.ImageUrl,
                            product.Price,
                            product.LastCheckedUtc)) >= collection.MinimumProductsForIndexing)
                    .Select(collection => CreateUrlElement(
                        ns,
                        superstoreOptions.BuildPublicUrl($"/{collection.ShopSlug}/{collection.Slug}"),
                        collection.UpdatedUtc)));
            }
        }

        var document = new XDocument(new XElement(ns + "urlset", urls));
        return Content(document.ToString(SaveOptions.DisableFormatting), "application/xml; charset=utf-8");
    }

    private static XElement CreateUrlElement(XNamespace ns, string location, DateTimeOffset lastModified) =>
        new(ns + "url",
            new XElement(ns + "loc", location),
            new XElement(ns + "lastmod", lastModified.ToString("yyyy-MM-dd")));

    private sealed record SitemapCandidate(
        string ShopSlug,
        string ProductId,
        string? EditorialTitle,
        string? EditorialDescription,
        string? ImageUrl,
        decimal? Price,
        DateTimeOffset LastCheckedUtc,
        DateTimeOffset LastRefreshedUtc);

    private sealed record SitemapCollection(
        Guid Id,
        string ShopSlug,
        string Slug,
        int MinimumProductsForIndexing,
        DateTimeOffset UpdatedUtc);

    private sealed record SitemapCollectionProduct(
        Guid CollectionId,
        string? EditorialTitle,
        string? EditorialDescription,
        string? ImageUrl,
        decimal? Price,
        DateTimeOffset LastCheckedUtc);
}
