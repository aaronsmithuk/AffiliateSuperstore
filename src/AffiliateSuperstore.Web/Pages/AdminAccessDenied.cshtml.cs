using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AffiliateSuperstore.Web.Pages;

[AllowAnonymous]
public sealed class AdminAccessDeniedModel : PageModel
{
    public void OnGet()
    {
    }
}
