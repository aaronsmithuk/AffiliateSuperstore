using System.ComponentModel.DataAnnotations;
using System.Net;
using AffiliateSuperstore.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Web.Pages;

[AllowAnonymous]
public sealed class AdminLoginModel(
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    IWebHostEnvironment environment) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public string ReturnUrl { get; private set; } = "/admin/api-test";
    public bool DevelopmentSetupAvailable { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl)
    {
        ReturnUrl = SafeReturnUrl(returnUrl);
        if (User.Identity?.IsAuthenticated == true && User.IsInRole(AdminAuthorization.RoleName))
        {
            return LocalRedirect(ReturnUrl);
        }

        await LoadSetupStateAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        ReturnUrl = SafeReturnUrl(returnUrl);
        await LoadSetupStateAsync();
        if (!ModelState.IsValid) return Page();

        var result = await signInManager.PasswordSignInAsync(
            Input.Username.Trim(),
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var user = await userManager.FindByNameAsync(Input.Username.Trim());
            if (user is not null && await userManager.IsInRoleAsync(user, AdminAuthorization.RoleName))
            {
                return LocalRedirect(ReturnUrl);
            }

            await signInManager.SignOutAsync();
        }

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "This account is temporarily locked after repeated failed attempts. Try again in 15 minutes."
            : "The administrator name or password was not recognised.");
        return Page();
    }

    private async Task LoadSetupStateAsync()
    {
        DevelopmentSetupAvailable = IsLocalDevelopmentRequest() && !await userManager.Users.AnyAsync();
    }

    private bool IsLocalDevelopmentRequest() =>
        environment.IsDevelopment() &&
        HttpContext.Connection.RemoteIpAddress is { } address &&
        IPAddress.IsLoopback(address);

    private bool IsSafeAdminReturnUrl(string value) =>
        Url.IsLocalUrl(value) && value.StartsWith("/admin", StringComparison.OrdinalIgnoreCase);

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && IsSafeAdminReturnUrl(returnUrl)
            ? returnUrl
            : "/admin/api-test";

    public sealed class LoginInput
    {
        [Required, StringLength(256), Display(Name = "Administrator name")]
        public string Username { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Keep me signed in")]
        public bool RememberMe { get; set; }
    }
}
