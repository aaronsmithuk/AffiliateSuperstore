using AffiliateSuperstore.Application.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AffiliateSuperstore.Web.Pages;

public sealed class RobotsModel(CatalogueSeoOptions seoOptions) : PageModel
{
    public IActionResult OnGet()
    {
        var origin = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        var rules = seoOptions.IndexingEnabled
            ? """
              Disallow: /admin/
              Disallow: /basket/
              Disallow: /go/
              Disallow: /health/
              Disallow: /Error
              """
            : "Disallow: /";
        var content = $"""
            User-agent: *
            {rules}

            Sitemap: {origin}/sitemap.xml
            """;
        return Content(content, "text/plain; charset=utf-8");
    }
}
