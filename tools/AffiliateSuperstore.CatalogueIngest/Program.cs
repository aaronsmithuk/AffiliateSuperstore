using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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

await using (var database = contextFactory.CreateDbContext())
{
    await database.Database.MigrateAsync();
}

await new ShopConfigurationSynchronizer(contextFactory, superstoreOptions, TimeProvider.System)
    .SynchronizeAsync();

using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AffiliateSuperstore-CatalogueIngest/0.1");
var client = new AliExpressClient(httpClient, aliExpressOptions, new AliExpressRequestSigner());
var service = new CatalogueIngestionService(
    new AliExpressCatalogueSource(client),
    contextFactory,
    TimeProvider.System);

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
