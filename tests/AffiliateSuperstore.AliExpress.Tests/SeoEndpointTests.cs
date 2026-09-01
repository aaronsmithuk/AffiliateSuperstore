using System.Xml.Linq;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Web.Hosting;
using AffiliateSuperstore.Web.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class SeoEndpointTests
{
    [Fact]
    public async Task Sitemap_IncludesStaticPublicPagesOnlyWhenIndexingIsEnabled()
    {
        var enabled = await GetSitemapLocationsAsync(indexingEnabled: true);
        var disabled = await GetSitemapLocationsAsync(indexingEnabled: false);

        Assert.Equal(
        [
            "https://wonderaisle.co.uk/",
            "https://wonderaisle.co.uk/about",
            "https://wonderaisle.co.uk/how-we-curate",
            "https://wonderaisle.co.uk/contact",
            "https://wonderaisle.co.uk/Privacy",
            "https://wonderaisle.co.uk/Terms"
        ], enabled);
        Assert.Empty(disabled);
    }

    [Fact]
    public void Robots_AllowsBasketCrawlSoNoindexCanBeRead()
    {
        var result = Assert.IsType<ContentResult>(new RobotsModel(
            new CatalogueSeoOptions { IndexingEnabled = true },
            CreateOptions()).OnGet());

        Assert.DoesNotContain("Disallow: /basket/", result.Content, StringComparison.Ordinal);
        Assert.Contains("Sitemap: https://wonderaisle.co.uk/sitemap.xml", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CanonicalOriginRedirect_RedirectsGetAndPreservesPathAndQuery()
    {
        var nextCalled = false;
        var middleware = new CanonicalOriginRedirectMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            CreateOptions());
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("www.wonderaisle.co.uk");
        context.Request.Path = "/plushies";
        context.Request.QueryString = new QueryString("?q=cow");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status308PermanentRedirect, context.Response.StatusCode);
        Assert.Equal("https://wonderaisle.co.uk/plushies?q=cow", context.Response.Headers.Location);
    }

    [Fact]
    public async Task CanonicalOriginRedirect_AllowsCanonicalGetAndNonGetRequests()
    {
        var calls = 0;
        var middleware = new CanonicalOriginRedirectMiddleware(
            _ =>
            {
                calls++;
                return Task.CompletedTask;
            },
            CreateOptions());

        var canonicalGet = new DefaultHttpContext();
        canonicalGet.Request.Method = HttpMethods.Get;
        canonicalGet.Request.Scheme = "https";
        canonicalGet.Request.Host = new HostString("wonderaisle.co.uk");
        await middleware.InvokeAsync(canonicalGet);

        var alternatePost = new DefaultHttpContext();
        alternatePost.Request.Method = HttpMethods.Post;
        alternatePost.Request.Scheme = "https";
        alternatePost.Request.Host = new HostString("www.wonderaisle.co.uk");
        await middleware.InvokeAsync(alternatePost);

        Assert.Equal(2, calls);
    }

    private static async Task<string[]> GetSitemapLocationsAsync(bool indexingEnabled)
    {
        var model = new SitemapModel(
            new InMemoryFactory(Guid.NewGuid().ToString("N")),
            new CatalogueSeoPolicy(TimeProvider.System),
            new CatalogueSeoOptions { IndexingEnabled = indexingEnabled },
            CreateOptions());

        var result = Assert.IsType<ContentResult>(await model.OnGetAsync(CancellationToken.None));
        var document = XDocument.Parse(result.Content!);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        return document.Descendants(ns + "loc").Select(element => element.Value).ToArray();
    }

    private static AffiliateSuperstoreOptions CreateOptions() => new()
    {
        CanonicalBaseUrl = "https://wonderaisle.co.uk",
        Shops =
        [
            new ShopDefinition
            {
                Slug = "plushies",
                DisplayName = "The Plushy Shop",
                PathPrefix = "/plushies",
                DefaultSearchQuery = "plush toy"
            }
        ]
    };

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }
}
