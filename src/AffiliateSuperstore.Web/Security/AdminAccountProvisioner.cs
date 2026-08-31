using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AffiliateSuperstore.Web.Security;

public sealed class AdminAccountProvisioner(
    IServiceScopeFactory scopeFactory,
    IOptions<AdminAuthenticationOptions> options,
    ILogger<AdminAccountProvisioner> logger)
{
    public async Task EnsureBootstrapAccountAsync(CancellationToken cancellationToken = default)
    {
        var username = options.Value.BootstrapUsername?.Trim();
        var password = options.Value.BootstrapPassword;
        if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("No configured admin bootstrap account. Development setup remains available until an administrator exists.");
            return;
        }
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "AdminAuthentication bootstrap configuration requires both BootstrapUsername and BootstrapPassword.");
        }

        using var scope = scopeFactory.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        if (!await roleManager.RoleExistsAsync(AdminAuthorization.RoleName))
        {
            EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(AdminAuthorization.RoleName)), "create the administrator role");
        }

        var user = await userManager.FindByNameAsync(username);
        if (user is null)
        {
            user = new IdentityUser
            {
                UserName = username,
                EmailConfirmed = true
            };
            EnsureSucceeded(await userManager.CreateAsync(user, password), "create the bootstrap administrator");
            logger.LogInformation("Created configured bootstrap administrator {Username}.", username);
        }

        if (!await userManager.IsInRoleAsync(user, AdminAuthorization.RoleName))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(user, AdminAuthorization.RoleName), "assign the administrator role");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded) return;
        var errors = string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
        throw new InvalidOperationException($"Unable to {operation}: {errors}");
    }
}
