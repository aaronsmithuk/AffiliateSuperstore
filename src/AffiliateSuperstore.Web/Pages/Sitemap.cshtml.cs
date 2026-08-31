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
}
