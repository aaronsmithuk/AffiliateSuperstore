using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
var webRoot = Path.Combine(repositoryRoot, "src", "AffiliateSuperstore.Web");
var configuration = new ConfigurationBuilder()
    .SetBasePath(webRoot)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: false)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var aiOptions = new AiAutomationOptions();
configuration.GetSection(AiAutomationOptions.SectionName).Bind(aiOptions);
if (!aiOptions.IsAvailable)
{
    Console.Error.WriteLine(aiOptions.AvailabilityMessage);
    return 1;
}

var connectionString = configuration.GetConnectionString("AffiliateSuperstore");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("ConnectionStrings:AffiliateSuperstore is not configured.");
    return 1;
}

var dbOptions = new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
    .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
    .Options;
var contextFactory = new LocalContextFactory(dbOptions);
using var httpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(Math.Clamp(aiOptions.TimeoutSeconds, 10, 120))
};
var audit = new AiInvocationAuditService(contextFactory, aiOptions, TimeProvider.System);
var provider = new OpenAiStructuredSuggestionProvider(httpClient, aiOptions, audit);
var validator = new EditorialContentValidator();
var quality = new ProductQualityAssessmentService(contextFactory, TimeProvider.System);
var suggestionService = new CatalogueAiSuggestionService(
    contextFactory,
    provider,
    validator,
    audit,
    aiOptions);

if (args.Contains("--prepare-queue", StringComparer.OrdinalIgnoreCase))
{
    var editorial = new CatalogueEditorialService(
        contextFactory,
        quality,
        validator,
        TimeProvider.System);
    var queue = new CatalogueAiQueuePreparationService(
        contextFactory,
        suggestionService,
        editorial,
        quality,
        aiOptions);
    Console.WriteLine($"AI approval-queue preparation: model {aiOptions.Model}, maximum {CatalogueAiQueuePreparationService.MaximumBatchSize} products");
    var queueResult = await queue.RunAsync(
        "plushies",
        CatalogueAiQueuePreparationService.MaximumBatchSize,
        "local CLI administrator");
    Console.WriteLine(queueResult.Message);
    Console.WriteLine($"Selected {queueResult.SelectedCount}; drafts {queueResult.DraftsSaved}; warnings {queueResult.WarningCount}; blocked {queueResult.BlockedCount}; failed {queueResult.FailedCount}; cached {queueResult.CacheHitCount}");
    Console.WriteLine($"Tokens: {queueResult.InputTokens} input / {queueResult.OutputTokens} output; estimated cost USD {queueResult.EstimatedCostUsd:F8}");
    foreach (var item in queueResult.Items)
    {
        var status = item.DraftSaved ? "DRAFT SAVED" : item.SuggestionResult.IsBlocked ? "BLOCKED" : "SKIPPED";
        Console.WriteLine($"[{status}] {item.ProductId} | {item.Outcome}");
    }
    return queueResult.FailedCount == 0 ? 0 : 2;
}

Console.WriteLine($"AI shadow sample: model {aiOptions.Model}, maximum {CatalogueAiSuggestionService.MaximumShadowSampleSize} products");
var result = await suggestionService.RunShadowAsync("plushies", CatalogueAiSuggestionService.MaximumShadowSampleSize);
Console.WriteLine(result.Message);
Console.WriteLine($"Selected {result.SelectedCount}; reviewable {result.SucceededCount}; blocked {result.BlockedCount}; failed {result.FailedCount}; cached {result.CacheHitCount}");
Console.WriteLine($"Tokens: {result.InputTokens} input / {result.OutputTokens} output; estimated cost USD {result.EstimatedCostUsd:F8}");

foreach (var item in result.Items)
{
    var status = item.Result.Succeeded ? "REVIEW" : item.Result.IsBlocked ? "BLOCKED" : "FAILED";
    Console.WriteLine();
    Console.WriteLine($"[{status}] {item.ProductId} | USD {item.EstimatedCostUsd:F8}{(item.Result.Suggestion?.WasCached == true ? " | cached" : string.Empty)}");
    Console.WriteLine($"Source: {item.SourceTitle}");
    if (item.Result.Suggestion is { } suggestion)
    {
        Console.WriteLine($"Title: {suggestion.SuggestedTitle}");
        Console.WriteLine($"Description: {suggestion.SuggestedDescription}");
    }
    foreach (var finding in item.Result.Findings ?? [])
    {
        Console.WriteLine($"Finding: {finding.Code} - {finding.Message}");
    }
    if (!item.Result.Succeeded) Console.WriteLine($"Result: {item.Result.Message}");
}

return result.FailedCount == 0 ? 0 : 2;

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

internal sealed class LocalContextFactory(DbContextOptions<AffiliateSuperstoreDbContext> options)
    : IDbContextFactory<AffiliateSuperstoreDbContext>
{
    public AffiliateSuperstoreDbContext CreateDbContext() => new(options);
}
