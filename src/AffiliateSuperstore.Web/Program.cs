using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Application.Basket;
using AffiliateSuperstore.Application.Tracking;
using AffiliateSuperstore.Application.Orders;
using AffiliateSuperstore.Application.Reporting;
using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Core.Tracking;
using AffiliateSuperstore.Core.Legal;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Web.Components;
using AffiliateSuperstore.Web.Hosting;
using AffiliateSuperstore.Web.Security;
using AffiliateSuperstore.Web.Services;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName("AffiliateSuperstore");
var dataProtectionKeysPath = builder.Configuration["Hosting:DataProtectionKeysPath"]?.Trim();
if (string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Hosting:DataProtectionKeysPath must point to a persistent, private directory in production.");
    }
}
else
{
    var keysDirectory = Path.GetFullPath(dataProtectionKeysPath, builder.Environment.ContentRootPath);
    var webRoot = builder.Environment.WebRootPath;
    var normalizedWebRoot = string.IsNullOrWhiteSpace(webRoot) ? null : Path.GetFullPath(webRoot);
    if (normalizedWebRoot is not null &&
        (keysDirectory.Equals(normalizedWebRoot, StringComparison.OrdinalIgnoreCase) ||
         keysDirectory.StartsWith(
             normalizedWebRoot + Path.DirectorySeparatorChar,
             StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException(
            "Hosting:DataProtectionKeysPath must not be inside the public web root.");
    }

    Directory.CreateDirectory(keysDirectory);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));
}

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services
    .AddOptions<AdminAuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(AdminAuthenticationOptions.SectionName));

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
var legalNoticeOptions = builder.Configuration
    .GetSection(LegalNoticeOptions.SectionName)
    .Get<LegalNoticeOptions>() ?? new LegalNoticeOptions();
if (!builder.Environment.IsDevelopment())
{
    legalNoticeOptions.ValidateForProduction();
}
builder.Services.AddSingleton(legalNoticeOptions);
var webAnalyticsOptions = builder.Configuration
    .GetSection(WebAnalyticsOptions.SectionName)
    .Get<WebAnalyticsOptions>() ?? new WebAnalyticsOptions();
builder.Services.AddSingleton(webAnalyticsOptions);
builder.Services.AddSingleton<IShopResolver, ShopResolver>();
builder.Services.AddSingleton<IClickIdGenerator, GuidClickIdGenerator>();
builder.Services.AddSingleton<AffiliateTrackingService>();
builder.Services.AddSingleton(TimeProvider.System);
var aiAutomationOptions = builder.Configuration
    .GetSection(AiAutomationOptions.SectionName)
    .Get<AiAutomationOptions>() ?? new AiAutomationOptions();
builder.Services.AddSingleton(aiAutomationOptions);
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
    builder.Services
        .AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.SignIn.RequireConfirmedAccount = false;
            options.User.RequireUniqueEmail = false;
        })
        .AddEntityFrameworkStores<AffiliateSuperstoreDbContext>()
        .AddDefaultTokenProviders();
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.Cookie.Name = "AffiliateSuperstore.Admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.LoginPath = "/admin/login";
        options.AccessDeniedPath = "/admin/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
    builder.Services.Configure<SecurityStampValidatorOptions>(options =>
        options.ValidationInterval = TimeSpan.FromMinutes(15));
    builder.Services.AddSingleton<DatabaseStatusService>();
    builder.Services.AddSingleton<ShopConfigurationSynchronizer>();
    builder.Services.AddSingleton<AdminAccountProvisioner>();
}
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AdminAuthorization.PolicyName, policy =>
        policy.RequireRole(AdminAuthorization.RoleName));
builder.Services.AddHttpClient<IAliExpressClient, AliExpressClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AffiliateSuperstore-ApiLab/0.1");
});
builder.Services.AddTransient<IAffiliateCatalogueSource, AliExpressCatalogueSource>();
builder.Services.AddTransient<IAffiliateProductDetailSource, AliExpressProductDetailSource>();
builder.Services.AddTransient<CatalogueIngestionService>();
builder.Services.AddTransient<CatalogueProductEnrichmentService>();
builder.Services.AddTransient<CatalogueDiscoveryPlanService>();
builder.Services.AddTransient<CatalogueReadinessService>();
builder.Services.AddTransient<AffiliateLinkRenewalService>();
builder.Services.AddTransient<ProductQualityAssessmentService>();
builder.Services.AddHttpClient<ProductImageFingerprintService>(client => client.Timeout = TimeSpan.FromSeconds(20))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None
    });
builder.Services.AddTransient<ProductIdentityService>();
builder.Services.AddTransient<ProductIdentityCalibrationService>();
builder.Services.AddTransient<EditorialContentValidator>();
builder.Services.AddTransient<CatalogueEditorialService>();
builder.Services.AddTransient<AiInvocationAuditService>();
builder.Services.AddHttpClient<OpenAiStructuredSuggestionProvider>(client =>
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(aiAutomationOptions.TimeoutSeconds, 10, 120)));
builder.Services.AddSingleton<UnavailableStructuredSuggestionProvider>();
builder.Services.AddTransient<IStructuredSuggestionProvider>(serviceProvider =>
    aiAutomationOptions.IsOpenAi
        ? serviceProvider.GetRequiredService<OpenAiStructuredSuggestionProvider>()
        : serviceProvider.GetRequiredService<UnavailableStructuredSuggestionProvider>());
builder.Services.AddTransient<CatalogueAiSuggestionService>();
builder.Services.AddTransient<CatalogueSeoPolicy>();
builder.Services.AddTransient<OutboundRedirectService>();
builder.Services.AddTransient<AffiliateOrderReconciliationService>();
builder.Services.AddTransient<AffiliateS2sIngestionService>();
builder.Services.AddTransient<OrderArchiveExportService>();
builder.Services.AddTransient<AffiliatePerformanceService>();
builder.Services.AddTransient<AffiliateImpressionService>();
builder.Services.AddSingleton<AnonymousBasketCodec>();
builder.Services.AddSingleton<AnonymousBasketStore>();
builder.Services.AddSingleton<CatalogueAutomationWakeSignal>();
if (!string.IsNullOrWhiteSpace(databaseConnection))
{
    builder.Services.AddSingleton<AutomationWorkQueueService>();
    builder.Services.AddSingleton<CatalogueAutomationMonitorService>();
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
}

if (!string.IsNullOrWhiteSpace(databaseConnection))
{
    await app.Services.GetRequiredService<ShopConfigurationSynchronizer>().SynchronizeAsync();
    await app.Services.GetRequiredService<AdminAccountProvisioner>().EnsureBootstrapAccountAsync();
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

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/TermsAndConditions", () => Results.Redirect("/Terms", permanent: true));
app.MapGet("/PrivacyPolicy", () => Results.Redirect("/Privacy", permanent: true));
app.MapRazorPages()
   .WithStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapOperationalHealthEndpoints();
app.MapAffiliateImpressionEndpoint();

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
}).RequireAuthorization(AdminAuthorization.PolicyName);

app.Run();
