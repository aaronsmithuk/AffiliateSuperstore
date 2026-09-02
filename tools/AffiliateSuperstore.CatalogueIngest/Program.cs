using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net;

var repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
var configuration = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(repositoryRoot, "src", "AffiliateSuperstore.Web", "appsettings.json"), optional: false)
    .AddJsonFile(Path.Combine(repositoryRoot, "src", "AffiliateSuperstore.Web", "appsettings.Development.json"), optional: false)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var aliExpressOptions = new AliExpressOptions();
configuration.GetSection(AliExpressOptions.SectionName).Bind(aliExpressOptions);
var superstoreOptions = new AffiliateSuperstoreOptions();
configuration.GetSection(AffiliateSuperstoreOptions.SectionName).Bind(superstoreOptions);
var connectionString = configuration.GetConnectionString("AffiliateSuperstore")
    ?? throw new InvalidOperationException("ConnectionStrings:AffiliateSuperstore is not configured.");
var dbOptions = new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
    .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
    .Options;
var contextFactory = new LocalDbContextFactory(dbOptions);
var previewSource = args.Length > 0 && string.Equals(args[0], "--preview-hot", StringComparison.OrdinalIgnoreCase)
    ? CatalogueDiscoverySource.HotProductQuery
    : args.Length > 0 && string.Equals(args[0], "--preview-smart", StringComparison.OrdinalIgnoreCase)
        ? CatalogueDiscoverySource.SmartMatch
        : (CatalogueDiscoverySource?)null;

if (previewSource is null)
{
    await using (var database = contextFactory.CreateDbContext())
    {
        await database.Database.MigrateAsync();
    }

    await new ShopConfigurationSynchronizer(contextFactory, superstoreOptions, TimeProvider.System)
        .SynchronizeAsync();
}

if (args.Length == 1 && string.Equals(args[0], "--identity", StringComparison.OrdinalIgnoreCase))
{
    using var imageClient = new HttpClient(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None
    }) { Timeout = TimeSpan.FromSeconds(20) };
    var fingerprintService = new ProductImageFingerprintService(imageClient, contextFactory, TimeProvider.System);
    var fingerprints = await fingerprintService.RefreshAsync("plushies", maximumProducts: 100);
    var identity = await new ProductIdentityService(contextFactory, TimeProvider.System).RebuildAsync("plushies");
    Console.WriteLine($"Images selected: {fingerprints.ProductsSelected}; new/changed hashes: {fingerprints.FingerprintsCreated}; unchanged: {fingerprints.FingerprintsUnchanged}; failed/skipped: {fingerprints.FailedOrSkipped}");
    Console.WriteLine($"Identity profiles updated: {identity.ProfilesUpdated}; candidates created: {identity.CandidatesCreated}; candidates refreshed: {identity.CandidatesUpdated}");
    return 0;
}

using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AffiliateSuperstore-CatalogueIngest/0.1");
var client = new AliExpressClient(httpClient, aliExpressOptions, new AliExpressRequestSigner());
var qualityAssessmentService = new ProductQualityAssessmentService(contextFactory, TimeProvider.System);
var catalogueSource = new AliExpressCatalogueSource(client);

if (previewSource is not null)
{
    var seedArgument = args.Skip(1).FirstOrDefault(argument =>
        argument.StartsWith("--seed=", StringComparison.OrdinalIgnoreCase));
    var seedProductId = seedArgument?["--seed=".Length..];
    var previewKeywords = string.Join(' ', args.Skip(1).Where(argument =>
        !argument.StartsWith("--seed=", StringComparison.OrdinalIgnoreCase)));
    if (string.IsNullOrWhiteSpace(previewKeywords)) previewKeywords = "plush toy";

    Console.WriteLine($"Running read-only {previewSource} preview for /plushies...");
    var preview = await new AdvancedCatalogueDiscoveryPreviewService(
            catalogueSource,
            contextFactory,
            qualityAssessmentService)
        .PreviewAsync("plushies", previewSource.Value, previewKeywords, seedProductId, pageSize: 20);
    Console.WriteLine($"Read: {preview.ProductsRead}; eligible: {preview.MinimallyEligible}; existing: {preview.AlreadyInCatalogue}; quality-clear new: {preview.QualityClearNewCandidates}");
    foreach (var candidate in preview.Candidates)
    {
        Console.WriteLine($"{candidate.RecommendedAction} | {candidate.ProductId} | {candidate.Currency} {candidate.SalePrice:N2} | {candidate.Title}");
    }

    return 0;
}

var service = new CatalogueIngestionService(
    catalogueSource,
    contextFactory,
    TimeProvider.System,
    qualityAssessmentService);

var keywords = args.Length > 0 ? string.Join(' ', args) : null;
Console.WriteLine("Running AliExpress catalogue ingestion for /plushies...");
var result = await service.RunAsync(new CatalogueIngestionRequest("plushies", keywords, PageSize: 20));
Console.WriteLine($"Job: {result.JobId}");
Console.WriteLine($"Status: {result.Status}");
Console.WriteLine($"Read: {result.ProductsRead}; written: {result.ProductsWritten}; rejected: {result.ProductsRejected}; links: {result.LinksCreatedOrRefreshed}");
if (!string.IsNullOrWhiteSpace(result.Error)) Console.WriteLine($"Error: {result.Error}");
return result.Status == IngestionJobStatus.Failed ? 1 : 0;

static string FindRepositoryRoot(string startPath)
{
    var directory = new DirectoryInfo(startPath);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "AffiliateSuperstore.slnx"))) return directory.FullName;
        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate AffiliateSuperstore.slnx.");
}

internal sealed class LocalDbContextFactory(DbContextOptions<AffiliateSuperstoreDbContext> options)
    : IDbContextFactory<AffiliateSuperstoreDbContext>
{
    public AffiliateSuperstoreDbContext CreateDbContext() => new(options);
}
