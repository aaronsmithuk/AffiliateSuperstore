using System.Security.Cryptography;
using System.Text;
using AffiliateSuperstore.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record ProductSuggestionFact(
    string Field,
    string Value,
    string Source);

public sealed record ProductEditorialSuggestionRequest(
    string ProductId,
    string SourceTitle,
    string? CurrentEditorialTitle,
    string? CurrentEditorialDescription,
    IReadOnlyList<ProductSuggestionFact> Facts,
    string PromptVersion,
    string InputHash);

public sealed record ProductEditorialSuggestionOutput(
    string SuggestedTitle,
    string SuggestedDescription,
    IReadOnlyList<string> Claims,
    IReadOnlyList<string> RemovedNoise,
    IReadOnlyList<string> Uncertainties,
    string Language,
    string Provider,
    string Model,
    string ResponseHash,
    int? InputTokens = null,
    int? OutputTokens = null,
    Guid? InvocationId = null,
    bool WasCached = false);

public interface IStructuredSuggestionProvider
{
    bool IsAvailable { get; }
    string AvailabilityMessage { get; }

    Task<ProductEditorialSuggestionOutput> SuggestProductCopyAsync(
        ProductEditorialSuggestionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableStructuredSuggestionProvider : IStructuredSuggestionProvider
{
    public bool IsAvailable => false;
    public string AvailabilityMessage =>
        "AI suggestions are disabled until a model provider, data-handling review and spend cap are configured.";

    public Task<ProductEditorialSuggestionOutput> SuggestProductCopyAsync(
        ProductEditorialSuggestionRequest request,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(AvailabilityMessage);
}

public sealed record CatalogueAiSuggestionResult(
    bool Succeeded,
    string Message,
    ProductEditorialSuggestionOutput? Suggestion = null,
    IReadOnlyList<EditorialValidationFinding>? Findings = null)
{
    public bool IsBlocked => Findings?.Any(item => item.Severity == EditorialFindingSeverity.Blocker) == true;
}

public sealed record CatalogueAiShadowItem(
    string ProductId,
    string SourceTitle,
    Persistence.Entities.ProductReviewStatus ReviewStatus,
    CatalogueAiSuggestionResult Result,
    decimal EstimatedCostUsd);

public sealed record CatalogueAiShadowRunResult(
    int RequestedCount,
    int SelectedCount,
    int CompletedCount,
    int SucceededCount,
    int BlockedCount,
    int FailedCount,
    int CacheHitCount,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    IReadOnlyList<CatalogueAiShadowItem> Items,
    string Message);

public sealed class CatalogueAiSuggestionService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    IStructuredSuggestionProvider provider,
    EditorialContentValidator editorialValidator,
    AiInvocationAuditService invocationAudit,
    AiAutomationOptions options)
{
    public const string PromptVersion = "product-editorial-v2";
    public const int MaximumShadowSampleSize = 10;

    public async Task<CatalogueAiSuggestionResult> SuggestAsync(
        string shopSlug,
        string productId,
        CancellationToken cancellationToken = default)
    {
        if (!provider.IsAvailable)
        {
            return Failure(provider.AvailabilityMessage);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await context.ShopProducts
            .AsNoTracking()
            .Include(candidate => candidate.Shop)
            .Include(candidate => candidate.Product)
            .SingleOrDefaultAsync(candidate => candidate.Shop.Slug == shopSlug && candidate.ProductId == productId, cancellationToken);
        if (item is null) return Failure("The catalogue product could not be found.");

        var facts = BuildFacts(item.Product);
        var inputHash = ComputeInputHash(item.ProductId, item.Product.Title, item.EditorialTitle, item.EditorialDescription, facts);
        var request = new ProductEditorialSuggestionRequest(
            item.ProductId,
            item.Product.Title,
            item.EditorialTitle,
            item.EditorialDescription,
            facts,
            PromptVersion,
            inputHash);

        ProductEditorialSuggestionOutput output;
        try
        {
            output = await provider.SuggestProductCopyAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Failure($"The AI provider could not produce a suggestion: {SafeMessage(exception.Message)}");
        }

        var title = Normalise(output.SuggestedTitle);
        var description = Normalise(output.SuggestedDescription);
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
        {
            return await ValidationFailureAsync(
                output,
                "ai.incomplete-copy",
                string.IsNullOrWhiteSpace(title) ? "title" : "description",
                "The AI provider returned an incomplete title or description.",
                cancellationToken);
        }
        if (title.Length > CatalogueEditorialService.MaximumTitleLength ||
            description.Length > CatalogueEditorialService.MaximumDescriptionLength)
        {
            return await ValidationFailureAsync(
                output,
                "ai.copy-length",
                title.Length > CatalogueEditorialService.MaximumTitleLength ? "title" : "description",
                "The AI suggestion exceeded the editorial length limits.",
                cancellationToken);
        }

        var suggestion = output with
        {
            SuggestedTitle = title,
            SuggestedDescription = description,
            Claims = CleanList(output.Claims),
            RemovedNoise = CleanList(output.RemovedNoise),
            Uncertainties = CleanList(output.Uncertainties),
            Language = Normalise(output.Language)
        };
        var validation = editorialValidator.Validate(new EditorialValidationInput(
            item.Product.Title,
            suggestion.SuggestedTitle,
            suggestion.SuggestedDescription));
        await invocationAudit.RecordValidationAsync(suggestion.InvocationId, validation, cancellationToken);

        if (validation.IsBlocked)
        {
            return new CatalogueAiSuggestionResult(
                false,
                "The AI draft was blocked because one or more claims lack source evidence. Nothing was changed.",
                suggestion,
                validation.Findings);
        }

        return new CatalogueAiSuggestionResult(
            true,
            validation.State == Persistence.Entities.EditorialValidationState.Warning
                ? "AI draft loaded for review with warnings. It has not been saved or published."
                : "AI draft loaded for review. It has not been saved or published.",
            suggestion,
            validation.Findings);
    }

    public async Task<CatalogueAiShadowRunResult> RunShadowAsync(
        string shopSlug,
        int requestedCount = MaximumShadowSampleSize,
        CancellationToken cancellationToken = default)
    {
        var sampleSize = Math.Clamp(requestedCount, 1, MaximumShadowSampleSize);
        if (!provider.IsAvailable)
        {
            return EmptyShadowResult(sampleSize, provider.AvailabilityMessage);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await context.ShopProducts
            .AsNoTracking()
            .Where(item => item.Shop.Slug == shopSlug &&
                item.IsActive &&
                item.Product.IsEligible &&
                item.Product.AvailabilityState == Persistence.Entities.ProductAvailabilityState.Available &&
                item.ReviewStatus != Persistence.Entities.ProductReviewStatus.Rejected)
            .OrderBy(item => item.ReviewStatus == Persistence.Entities.ProductReviewStatus.NeedsReview ? 0 :
                item.ReviewStatus == Persistence.Entities.ProductReviewStatus.Pending ? 1 : 2)
            .ThenByDescending(item => item.Product.LastRefreshedUtc)
            .ThenBy(item => item.ProductId)
            .Take(sampleSize)
            .Select(item => new ShadowCandidate(item.ProductId, item.Product.Title, item.ReviewStatus))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return EmptyShadowResult(sampleSize, "No eligible active products were available for the AI shadow sample.");
        }

        var items = new List<CatalogueAiShadowItem>(candidates.Count);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await SuggestAsync(shopSlug, candidate.ProductId, cancellationToken);
            var suggestion = result.Suggestion;
            var estimatedCost = options.EstimateCostUsd(suggestion?.InputTokens ?? 0, suggestion?.OutputTokens ?? 0);
            items.Add(new CatalogueAiShadowItem(
                candidate.ProductId,
                candidate.SourceTitle,
                candidate.ReviewStatus,
                result,
                estimatedCost));
        }

        return new CatalogueAiShadowRunResult(
            sampleSize,
            candidates.Count,
            items.Count,
            items.Count(item => item.Result.Succeeded),
            items.Count(item => item.Result.IsBlocked),
            items.Count(item => !item.Result.Succeeded && !item.Result.IsBlocked),
            items.Count(item => item.Result.Suggestion?.WasCached == true),
            items.Sum(item => item.Result.Suggestion?.InputTokens ?? 0),
            items.Sum(item => item.Result.Suggestion?.OutputTokens ?? 0),
            items.Sum(item => item.EstimatedCostUsd),
            items,
            "Shadow run complete. No catalogue copy was saved, approved or published.");
    }

    private static IReadOnlyList<ProductSuggestionFact> BuildFacts(Persistence.Entities.ProductRecord product)
    {
        var facts = new List<ProductSuggestionFact>
        {
            new("sourceTitle", product.Title, "AliExpress product record")
        };
        AddFact(facts, "firstCategory", product.FirstLevelCategoryName);
        AddFact(facts, "secondCategory", product.SecondLevelCategoryName);
        AddFact(facts, "sellerName", product.SellerName);
        AddFact(facts, "sku", product.SkuId);
        AddFact(facts, "ean", product.EanCode);
        return facts;
    }

    private static void AddFact(ICollection<ProductSuggestionFact> facts, string field, string? value)
    {
        value = Normalise(value);
        if (!string.IsNullOrWhiteSpace(value)) facts.Add(new(field, value, "AliExpress product record"));
    }

    private static string ComputeInputHash(
        string productId,
        string sourceTitle,
        string? editorialTitle,
        string? editorialDescription,
        IReadOnlyList<ProductSuggestionFact> facts)
    {
        var canonical = string.Join('\n', new[]
        {
            PromptVersion,
            productId,
            sourceTitle,
            editorialTitle ?? string.Empty,
            editorialDescription ?? string.Empty,
            string.Join('\n', facts.Select(fact => $"{fact.Field}={fact.Value}|{fact.Source}"))
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string Normalise(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static IReadOnlyList<string> CleanList(IReadOnlyList<string>? values) =>
        values?.Select(Normalise).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToArray() ?? [];

    private static string SafeMessage(string message)
    {
        var safe = Normalise(message);
        return safe.Length <= 300 ? safe : safe[..300];
    }

    private async Task<CatalogueAiSuggestionResult> ValidationFailureAsync(
        ProductEditorialSuggestionOutput output,
        string code,
        string field,
        string message,
        CancellationToken cancellationToken)
    {
        EditorialValidationFinding[] findings =
        [
            new(code, EditorialFindingSeverity.Blocker, field, message)
        ];
        await invocationAudit.RecordValidationAsync(
            output.InvocationId,
            new EditorialValidationResult(Persistence.Entities.EditorialValidationState.Blocked, findings),
            cancellationToken);
        return new CatalogueAiSuggestionResult(false, message, output, findings);
    }

    private static CatalogueAiSuggestionResult Failure(string message) => new(false, message);

    private static CatalogueAiShadowRunResult EmptyShadowResult(int requestedCount, string message) =>
        new(requestedCount, 0, 0, 0, 0, 0, 0, 0, 0, 0m, [], message);

    private sealed record ShadowCandidate(
        string ProductId,
        string SourceTitle,
        Persistence.Entities.ProductReviewStatus ReviewStatus);
}
