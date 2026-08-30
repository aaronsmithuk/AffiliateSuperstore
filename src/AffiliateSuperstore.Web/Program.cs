using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Application.Basket;
using AffiliateSuperstore.Application.Tracking;
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
builder.Services.AddTransient<OutboundRedirectService>();
builder.Services.AddSingleton<AnonymousBasketCodec>();
builder.Services.AddSingleton<AnonymousBasketStore>();
if (!string.IsNullOrWhiteSpace(databaseConnection))
{
    builder.Services.AddHostedService<CatalogueAutomationWorker>();
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

app.Run();
