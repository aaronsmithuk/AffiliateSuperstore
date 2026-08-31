using AffiliateSuperstore.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AffiliateSuperstore.Web.Pages;

[Authorize(Policy = AdminAuthorization.PolicyName)]
public sealed class AdminLogoutModel(SignInManager<IdentityUser> signInManager) : PageModel
{
    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await signInManager.SignOutAsync();
        return LocalRedirect("/admin/login");
    }
}
