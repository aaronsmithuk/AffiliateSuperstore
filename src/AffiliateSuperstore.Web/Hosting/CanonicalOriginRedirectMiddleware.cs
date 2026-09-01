using AffiliateSuperstore.Core.Shops;
using Microsoft.AspNetCore.Http.Extensions;

namespace AffiliateSuperstore.Web.Hosting;

public sealed class CanonicalOriginRedirectMiddleware
{
    private readonly RequestDelegate next;
    private readonly string canonicalScheme;
    private readonly HostString canonicalHost;

    public CanonicalOriginRedirectMiddleware(
        RequestDelegate next,
        AffiliateSuperstoreOptions superstoreOptions)
    {
        this.next = next;

        if (!Uri.TryCreate(superstoreOptions.CanonicalBaseUrl, UriKind.Absolute, out var canonicalOrigin))
        {
            throw new InvalidOperationException("Superstore:CanonicalBaseUrl must be an absolute URL.");
        }

        canonicalScheme = canonicalOrigin.Scheme;
        canonicalHost = canonicalOrigin.IsDefaultPort
            ? new HostString(canonicalOrigin.Host)
            : new HostString(canonicalOrigin.Host, canonicalOrigin.Port);
    }

    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            return next(context);
        }

        if (string.Equals(request.Scheme, canonicalScheme, StringComparison.OrdinalIgnoreCase) &&
            request.Host.Equals(canonicalHost))
        {
            return next(context);
        }

        var destination = UriHelper.BuildAbsolute(
            canonicalScheme,
            canonicalHost,
            request.PathBase,
            request.Path,
            request.QueryString);
        context.Response.Redirect(destination, permanent: true, preserveMethod: true);
        return Task.CompletedTask;
    }
}
