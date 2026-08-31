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
public sealed class AdminSetupModel(
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    SignInManager<IdentityUser> signInManager,
    IWebHostEnvironment environment) : PageModel
{
    [BindProperty]
    public SetupInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsLocalDevelopmentRequest()) return NotFound();
        if (await userManager.Users.AnyAsync()) return RedirectToPage("/AdminLogin");
        Input.Username = "aaronsmithmsc";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!IsLocalDevelopmentRequest()) return NotFound();
        if (await userManager.Users.AnyAsync()) return RedirectToPage("/AdminLogin");
        if (!ModelState.IsValid) return Page();

        var roleResult = await EnsureAdministratorRoleAsync();
        if (!roleResult.Succeeded)
        {
            AddErrors(roleResult);
            return Page();
        }

        var user = new IdentityUser
        {
            UserName = Input.Username.Trim(),
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(user, Input.Password);
        if (!createResult.Succeeded)
        {
            AddErrors(createResult);
            return Page();
        }

        var roleAssignment = await userManager.AddToRoleAsync(user, AdminAuthorization.RoleName);
        if (!roleAssignment.Succeeded)
        {
            await userManager.DeleteAsync(user);
            AddErrors(roleAssignment);
            return Page();
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect("/admin/api-test");
    }

    private async Task<IdentityResult> EnsureAdministratorRoleAsync()
    {
        if (await roleManager.RoleExistsAsync(AdminAuthorization.RoleName)) return IdentityResult.Success;
        return await roleManager.CreateAsync(new IdentityRole(AdminAuthorization.RoleName));
    }

    private bool IsLocalDevelopmentRequest() =>
        environment.IsDevelopment() &&
        HttpContext.Connection.RemoteIpAddress is { } address &&
        IPAddress.IsLoopback(address);

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }

    public sealed class SetupInput
    {
        [Required, StringLength(256), Display(Name = "Administrator name")]
        public string Username { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
