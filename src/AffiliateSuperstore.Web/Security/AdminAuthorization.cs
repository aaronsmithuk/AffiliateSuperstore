namespace AffiliateSuperstore.Web.Security;

public static class AdminAuthorization
{
    public const string PolicyName = "AdminOnly";
    public const string RoleName = "Administrator";
}

public sealed class AdminAuthenticationOptions
{
    public const string SectionName = "AdminAuthentication";

    public string? BootstrapUsername { get; init; }
    public string? BootstrapPassword { get; init; }
}
