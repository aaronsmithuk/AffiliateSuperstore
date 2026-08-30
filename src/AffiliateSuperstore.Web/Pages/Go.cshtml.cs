using AffiliateSuperstore.Application.Tracking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AffiliateSuperstore.Web.Pages;

public sealed class GoModel(OutboundRedirectService redirectService) : PageModel
{
    public async Task<IActionResult> OnGetAsync(
        string shopSlug,
        string productId,
        string? placement,
        CancellationToken cancellationToken)
    {
        var result = await redirectService.CreateAsync(shopSlug, productId, placement, cancellationToken: cancellationToken);
        return result is null ? NotFound() : Redirect(result.Url);
    }
}
