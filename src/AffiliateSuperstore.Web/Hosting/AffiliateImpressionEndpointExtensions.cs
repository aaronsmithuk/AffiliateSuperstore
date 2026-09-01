using AffiliateSuperstore.Application.Reporting;
using Microsoft.AspNetCore.Antiforgery;

namespace AffiliateSuperstore.Web.Hosting;

public static class AffiliateImpressionEndpointExtensions
{
    public static IEndpointRouteBuilder MapAffiliateImpressionEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/analytics/impressions", async (
            HttpContext httpContext,
            IAntiforgery antiforgery,
            AffiliateImpressionService impressionService,
            AffiliateImpressionBatchRequest request,
            CancellationToken cancellationToken) =>
        {
            httpContext.Response.Headers.CacheControl = "no-store";
            try
            {
                await antiforgery.ValidateRequestAsync(httpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest(new { error = "The request could not be validated." });
            }

            if (request.Items is null ||
                request.Items.Count == 0 ||
                request.Items.Count > AffiliateImpressionService.MaximumBatchSize)
            {
                return Results.BadRequest(new { error = "Supply between 1 and 64 impressions." });
            }

            var result = await impressionService.RecordAsync(
                request.Items.Select(item => new AffiliateImpressionInput(
                    item.Shop ?? string.Empty,
                    item.ProductId ?? string.Empty,
                    item.Placement ?? string.Empty)).ToArray(),
                cancellationToken);

            return Results.Ok(new { result.Accepted, result.Rejected });
        })
            .AllowAnonymous()
            .WithName("RecordAffiliateImpressions");

        return endpoints;
    }

}

public sealed record AffiliateImpressionBatchRequest(IReadOnlyList<AffiliateImpressionItemRequest>? Items);

public sealed record AffiliateImpressionItemRequest(string? Shop, string? ProductId, string? Placement);
