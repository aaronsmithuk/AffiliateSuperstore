using AffiliateSuperstore.Application.Orders;

namespace AffiliateSuperstore.Web.Hosting;

public static class AffiliateS2sEndpointExtensions
{
    public static IEndpointRouteBuilder MapAffiliateS2sEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods(
                "/integrations/aliexpress/s2s",
                [HttpMethods.Get, HttpMethods.Post],
                HandleAsync)
            .AllowAnonymous()
            .WithName("IngestAliExpressAffiliateS2sEvent");

        return endpoints;
    }

    public static async Task<IResult> HandleAsync(
        HttpRequest request,
        AffiliateS2sIngestionService ingestion,
        CancellationToken cancellationToken)
    {
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        if (!ingestion.IsEnabled) return Results.NotFound();
        if (!ingestion.IsConfigured) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in request.Query)
        {
            if (pair.Value.Count > 0) values[pair.Key] = pair.Value[0] ?? string.Empty;
        }

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(cancellationToken);
            foreach (var pair in form)
            {
                if (pair.Value.Count > 0) values[pair.Key] = pair.Value[0] ?? string.Empty;
            }
        }

        values.TryGetValue("verification_token", out var suppliedToken);
        if (!ingestion.IsAuthorized(suppliedToken)) return Results.Unauthorized();
        values.Remove("verification_token");

        var result = await ingestion.IngestAsync(values, cancellationToken);
        return result.Disposition == AffiliateS2sDisposition.Rejected
            ? Results.BadRequest(new { error = result.Error })
            : Results.Text("ok", "text/plain");
    }
}
