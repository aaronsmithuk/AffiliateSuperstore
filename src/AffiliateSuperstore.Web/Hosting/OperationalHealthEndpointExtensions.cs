using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AffiliateSuperstore.Web.Hosting;

public static class OperationalHealthEndpointExtensions
{
    public static IEndpointRouteBuilder MapOperationalHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", (HttpResponse response, TimeProvider timeProvider) =>
        {
            response.Headers.CacheControl = "no-store";
            return Results.Ok(new OperationalHealthResponse("healthy", "application", timeProvider.GetUtcNow()));
        }).AllowAnonymous();

        endpoints.MapGet("/health/ready", (
            HttpResponse response,
            IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
            CheckDatabaseAsync("readiness", response, contextFactory, timeProvider, cancellationToken))
            .AllowAnonymous();

        endpoints.MapGet("/health/wake", (
            HttpResponse response,
            IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
            TimeProvider timeProvider,
            CatalogueAutomationWakeSignal wakeSignal,
            IOptions<CatalogueAutomationOptions> automationOptions,
            CancellationToken cancellationToken) =>
        {
            if (automationOptions.Value.Enabled) wakeSignal.Signal();
            return CheckDatabaseAsync("scheduled-wake", response, contextFactory, timeProvider, cancellationToken);
        })
            .AllowAnonymous();

        return endpoints;
    }

    private static async Task<IResult> CheckDatabaseAsync(
        string check,
        HttpResponse response,
        IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        response.Headers.CacheControl = "no-store";
        try
        {
            await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
            var canConnect = await database.Database.CanConnectAsync(cancellationToken);
            var status = canConnect ? "healthy" : "unavailable";
            var result = new OperationalHealthResponse(status, check, timeProvider.GetUtcNow());
            return canConnect
                ? Results.Ok(result)
                : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Results.Json(
                new OperationalHealthResponse("unavailable", check, timeProvider.GetUtcNow()),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private sealed record OperationalHealthResponse(
        string Status,
        string Check,
        DateTimeOffset CheckedUtc);
}
