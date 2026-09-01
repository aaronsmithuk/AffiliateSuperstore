using System.Security.Cryptography;
using System.Text;
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
            HttpRequest request,
            HttpResponse response,
            IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
            TimeProvider timeProvider,
            CatalogueAutomationWakeSignal wakeSignal,
            IOptions<CatalogueAutomationOptions> automationOptions,
            CancellationToken cancellationToken) =>
        {
            response.Headers.CacheControl = "no-store";
            var options = automationOptions.Value;
            if (!options.Enabled)
            {
                return Task.FromResult<IResult>(Results.Json(
                    new ScheduledWakeResponse("disabled", false, timeProvider.GetUtcNow()),
                    statusCode: StatusCodes.Status503ServiceUnavailable));
            }
            if (string.IsNullOrWhiteSpace(options.WakeToken))
            {
                return Task.FromResult<IResult>(Results.Json(
                    new ScheduledWakeResponse("misconfigured", false, timeProvider.GetUtcNow()),
                    statusCode: StatusCodes.Status503ServiceUnavailable));
            }

            var suppliedToken = request.Headers["X-Automation-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(suppliedToken))
            {
                suppliedToken = request.Query["key"].FirstOrDefault();
            }
            if (!SecretsEqual(options.WakeToken, suppliedToken))
            {
                return Task.FromResult<IResult>(Results.Unauthorized());
            }

            wakeSignal.Signal();
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

    private sealed record ScheduledWakeResponse(
        string Status,
        bool Signalled,
        DateTimeOffset CheckedUtc);

    private static bool SecretsEqual(string expected, string? supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied)) return false;
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }
}
