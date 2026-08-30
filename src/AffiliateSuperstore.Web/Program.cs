using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Application.Basket;
using AffiliateSuperstore.Application.Tracking;
using AffiliateSuperstore.Application.Orders;
using AffiliateSuperstore.Application.Reporting;
using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Core.Tracking;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Web.Components;
using AffiliateSuperstore.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddOptions<AliExpressOptions>()
    .Bind(builder.Configuration.GetSection(AliExpressOptions.SectionName));
builder.Services.AddSingleton(serviceProvider =>
    serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AliExpressOptions>>().Value);
builder.Services.AddSingleton<AliExpressRequestSigner>();
var superstoreOptions = builder.Configuration
    .GetSection(AffiliateSuperstoreOptions.SectionName)
    .Get<AffiliateSuperstoreOptions>() ?? new AffiliateSuperstoreOptions();
builder.Services.AddSingleton(superstoreOptions);
builder.Services.AddSingleton<IShopResolver, ShopResolver>();
builder.Services.AddSingleton<IClickIdGenerator, GuidClickIdGenerator>();
builder.Services.AddSingleton<AffiliateTrackingService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services
    .AddOptions<CatalogueAutomationOptions>()
    .Bind(builder.Configuration.GetSection(CatalogueAutomationOptions.SectionName));
builder.Services
    .AddOptions<CatalogueSeoOptions>()
    .Bind(builder.Configuration.GetSection(CatalogueSeoOptions.SectionName));
builder.Services.AddSingleton(serviceProvider =>
    serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CatalogueSeoOptions>>().Value);
builder.Services
    .AddOptions<OrderReconciliationOptions>()
    .Bind(builder.Configuration.GetSection(OrderReconciliationOptions.SectionName));
builder.Services.AddSingleton(serviceProvider =>
    serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OrderReconciliationOptions>>().Value);
builder.Services
    .AddOptions<AffiliateS2sOptions>()
    .Bind(builder.Configuration.GetSection(AffiliateS2sOptions.SectionName));
builder.Services.AddSingleton(serviceProvider =>
    serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AffiliateS2sOptions>>().Value);
var databaseConnection = builder.Configuration.GetConnectionString("AffiliateSuperstore");
if (string.IsNullOrWhiteSpace(databaseConnection))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "ConnectionStrings:AffiliateSuperstore must be supplied by the production host.");
    }
}
else
{
    builder.Services.AddPooledDbContextFactory<AffiliateSuperstoreDbContext>(options =>
        options.UseSqlServer(databaseConnection, sql => sql.EnableRetryOnFailure()));
    builder.Services.AddSingleton<DatabaseStatusService>();
    builder.Services.AddSingleton<ShopConfigurationSynchronizer>();
}
builder.Services.AddHttpClient<IAliExpressClient, AliExpressClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AffiliateSuperstore-ApiLab/0.1");
});
builder.Services.AddTransient<IAffiliateCatalogueSource, AliExpressCatalogueSource>();
builder.Services.AddTransient<CatalogueIngestionService>();
builder.Services.AddTransient<AffiliateLinkRenewalService>();
builder.Services.AddTransient<ProductQualityAssessmentService>();
builder.Services.AddTransient<CatalogueEditorialService>();
builder.Services.AddTransient<CatalogueSeoPolicy>();
builder.Services.AddTransient<OutboundRedirectService>();
builder.Services.AddTransient<AffiliateOrderReconciliationService>();
builder.Services.AddTransient<AffiliateS2sIngestionService>();
builder.Services.AddTransient<OrderArchiveExportService>();
builder.Services.AddTransient<AffiliatePerformanceService>();
builder.Services.AddSingleton<AnonymousBasketCodec>();
builder.Services.AddSingleton<AnonymousBasketStore>();
if (!string.IsNullOrWhiteSpace(databaseConnection))
{
    builder.Services.AddHostedService<CatalogueAutomationWorker>();
    builder.Services.AddHostedService<OrderReconciliationWorker>();
}

var app = builder.Build();

if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(databaseConnection))
{
    var contextFactory = app.Services.GetRequiredService<IDbContextFactory<AffiliateSuperstoreDbContext>>();
    await using (var database = await contextFactory.CreateDbContextAsync())
    {
        await database.Database.MigrateAsync();
    }

    await app.Services.GetRequiredService<ShopConfigurationSynchronizer>().SynchronizeAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapMethods("/integrations/aliexpress/s2s", ["GET", "POST"], async (
    HttpRequest request,
    AffiliateS2sIngestionService ingestion,
    CancellationToken cancellationToken) =>
{
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
});

app.MapGet("/admin/orders/export.csv", async (
    IWebHostEnvironment environment,
    OrderArchiveExportService exportService,
    CancellationToken cancellationToken) =>
{
    if (!environment.IsDevelopment()) return Results.NotFound();
    var export = await exportService.CreateCsvAsync(cancellationToken);
    return Results.File(export.Content, "text/csv; charset=utf-8", export.FileName);
});

app.Run();
