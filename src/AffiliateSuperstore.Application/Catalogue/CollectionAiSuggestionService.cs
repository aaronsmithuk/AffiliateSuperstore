using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record CollectionSuggestionEvidence(
    string ProductId,
    string Title,
    string? FirstCategory,
    string? SecondCategory);

public sealed record CollectionSuggestionRequest(
    string ShopSlug,
    string ShopName,
    IReadOnlyList<string> ExistingCollections,
    IReadOnlyList<CollectionSuggestionEvidence> Products,
    int MaximumSuggestions,
    string PromptVersion,
    string InputHash);

public sealed record SuggestedCollectionDraft(
    string DisplayName,
    string ShortDescription,
    string IntroductoryCopy,
    string SeoTitle,
    string SeoDescription,
    IReadOnlyList<string> DiscoveryQueries,
    string Rationale,
    IReadOnlyList<string> EvidenceProductIds);

public sealed record CollectionSuggestionOutput(
    IReadOnlyList<SuggestedCollectionDraft> Suggestions,
    string Provider,
    string Model,
    string ResponseHash,
    int? InputTokens = null,
    int? OutputTokens = null,
    Guid? InvocationId = null,
    bool WasCached = false);

public interface ICollectionSuggestionProvider
{
    bool IsAvailable { get; }
    string AvailabilityMessage { get; }

    Task<CollectionSuggestionOutput> SuggestCollectionsAsync(
        CollectionSuggestionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableCollectionSuggestionProvider : ICollectionSuggestionProvider
{
    public bool IsAvailable => false;
    public string AvailabilityMessage => "AI collection suggestions are disabled until a supported provider and spend cap are configured.";

    public Task<CollectionSuggestionOutput> SuggestCollectionsAsync(
        CollectionSuggestionRequest request,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(AvailabilityMessage);
}

public sealed record CollectionSuggestionDraftView(
    Guid Id,
    string ShopSlug,
    string DisplayName,
    string Slug,
    string ShortDescription,
    string IntroductoryCopy,
    string SeoTitle,
    string SeoDescription,
    IReadOnlyList<string> DiscoveryQueries,
    string Rationale,
    IReadOnlyList<string> EvidenceProductIds,
    CollectionSuggestionStatus Status,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ReviewedUtc,
    string? ReviewedBy);

public sealed record CollectionSuggestionRunResult(
    bool Succeeded,
    int ProductsConsidered,
    int SuggestionsReturned,
    int DraftsSaved,
    decimal EstimatedCostUsd,
    string Message);

public sealed class CollectionAiSuggestionService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    ICollectionSuggestionProvider provider,
    CatalogueCollectionService collectionService,
    ProductQualityAssessmentService qualityAssessmentService,
    AiAutomationOptions aiOptions,
    TimeProvider timeProvider)
{
    public const string PromptVersion = "collection-suggestions-v1";
    private const int MaximumEvidenceProducts = 120;
    private const int MinimumEvidenceProducts = 6;

    public async Task<IReadOnlyList<CollectionSuggestionDraftView>> GetAsync(
        string shopSlug,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.CollectionSuggestions.AsNoTracking()
            .Where(item => item.Shop.Slug == shopSlug)
            .OrderBy(item => item.Status == CollectionSuggestionStatus.Draft ? 0 : 1)
            .ThenByDescending(item => item.CreatedUtc)
            .Take(100)
            .Select(item => new
            {
                item.Id,
                ShopSlug = item.Shop.Slug,
                item.DisplayName,
                item.Slug,
                item.ShortDescription,
                item.IntroductoryCopy,
                item.SeoTitle,
                item.SeoDescription,
                item.DiscoveryQueriesJson,
                item.Rationale,
                item.EvidenceProductIdsJson,
                item.Status,
                item.CreatedUtc,
                item.ReviewedUtc,
                item.ReviewedBy
            })
            .ToArrayAsync(cancellationToken);
        return rows.Select(item => new CollectionSuggestionDraftView(
            item.Id,
            item.ShopSlug,
            item.DisplayName,
            item.Slug,
            item.ShortDescription,
            item.IntroductoryCopy,
            item.SeoTitle,
            item.SeoDescription,
            ReadStringArray(item.DiscoveryQueriesJson),
            item.Rationale,
            ReadStringArray(item.EvidenceProductIdsJson),
            item.Status,
            item.CreatedUtc,
            item.ReviewedUtc,
            item.ReviewedBy)).ToArray();
    }

    public async Task<CollectionSuggestionRunResult> GenerateAsync(
        string shopSlug,
        int maximumSuggestions = 3,
        CancellationToken cancellationToken = default)
    {
        maximumSuggestions = Math.Clamp(maximumSuggestions, 1, 5);
        if (!provider.IsAvailable)
        {
            return new(false, 0, 0, 0, 0m, provider.AvailabilityMessage);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var shop = await context.Shops.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Slug == shopSlug && item.IsEnabled, cancellationToken);
        if (shop is null) return new(false, 0, 0, 0, 0m, "The enabled shop was not found.");

        var existingCollections = await context.Collections.AsNoTracking()
            .Where(item => item.ShopId == shop.Id)
            .OrderBy(item => item.DisplayName)
            .Select(item => item.DisplayName)
            .ToArrayAsync(cancellationToken);
        var products = await context.ShopProducts.AsNoTracking()
            .Where(item => item.ShopId == shop.Id &&
                item.IsActive &&
                item.Product.IsEligible &&
                item.Product.AvailabilityState == ProductAvailabilityState.Available &&
                item.ReviewStatus != ProductReviewStatus.Rejected)
            .OrderByDescending(item => item.ReviewStatus == ProductReviewStatus.Approved)
            .ThenByDescending(item => item.Product.LastRefreshedUtc)
            .Take(MaximumEvidenceProducts)
            .Select(item => new CollectionSuggestionEvidence(
                item.ProductId,
                item.EditorialTitle ?? item.Product.Title,
                item.Product.FirstLevelCategoryName,
                item.Product.SecondLevelCategoryName))
            .ToArrayAsync(cancellationToken);
        if (products.Length < MinimumEvidenceProducts)
        {
            return new(false, products.Length, 0, 0, 0m,
                $"At least {MinimumEvidenceProducts} eligible products are required before suggesting collection drafts.");
        }

        var inputHash = ComputeInputHash(shopSlug, existingCollections, products, maximumSuggestions);
        var request = new CollectionSuggestionRequest(
            shopSlug,
            shop.DisplayName,
            existingCollections,
            products,
            maximumSuggestions,
            PromptVersion,
            inputHash);
        CollectionSuggestionOutput output;
        try
        {
            output = await provider.SuggestCollectionsAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new(false, products.Length, 0, 0, 0m,
                $"The AI provider could not produce collection suggestions: {SafeMessage(exception.Message)}");
        }

        var productIds = products.Select(item => item.ProductId).ToHashSet(StringComparer.Ordinal);
        var existingSlugs = (await context.Collections.AsNoTracking()
            .Where(item => item.ShopId == shop.Id)
            .Select(item => item.Slug)
            .ToArrayAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        existingSlugs.UnionWith(await context.CollectionSuggestions.AsNoTracking()
            .Where(item => item.ShopId == shop.Id && item.Status == CollectionSuggestionStatus.Draft)
            .Select(item => item.Slug)
            .ToArrayAsync(cancellationToken));

        var now = timeProvider.GetUtcNow();
        var saved = 0;
        foreach (var suggestion in output.Suggestions.Take(maximumSuggestions))
        {
            var displayName = Normalise(suggestion.DisplayName);
            var slug = Slugify(displayName);
            var evidenceIds = CleanList(suggestion.EvidenceProductIds)
                .Where(productIds.Contains)
                .Take(20)
                .ToArray();
            var queries = CleanList(suggestion.DiscoveryQueries).Take(CatalogueCollectionService.MaximumDiscoveryQueries).ToArray();
            var quality = string.IsNullOrWhiteSpace(displayName)
                ? new ProductQualityAssessment([new ProductQualityFlag("collection.missing-name", "The suggestion has no collection name.")])
                : qualityAssessmentService.Assess(displayName);
            if (slug.Length == 0 || existingSlugs.Contains(slug) || evidenceIds.Length < 3 || queries.Length == 0 || quality.RequiresReview)
            {
                continue;
            }

            var record = new CollectionSuggestionRecord
            {
                Id = Guid.CreateVersion7(),
                ShopId = shop.Id,
                AiInvocationId = output.InvocationId,
                DisplayName = Truncate(displayName, 160),
                Slug = Truncate(slug, 80),
                ShortDescription = Truncate(Normalise(suggestion.ShortDescription), 500),
                IntroductoryCopy = Truncate(Normalise(suggestion.IntroductoryCopy), 4000),
                SeoTitle = Truncate(Normalise(suggestion.SeoTitle), 200),
                SeoDescription = Truncate(Normalise(suggestion.SeoDescription), 500),
                DiscoveryQueriesJson = JsonSerializer.Serialize(queries),
                Rationale = Truncate(Normalise(suggestion.Rationale), 2000),
                EvidenceProductIdsJson = JsonSerializer.Serialize(evidenceIds),
                Status = CollectionSuggestionStatus.Draft,
                PromptVersion = PromptVersion,
                InputHash = inputHash,
                CreatedUtc = now
            };
            if (string.IsNullOrWhiteSpace(record.ShortDescription) ||
                record.DisplayName.Length is < 3 or > 160 ||
                record.ShortDescription.Length is < 30 or > 500 ||
                record.IntroductoryCopy.Length is < 120 or > 4000 ||
                record.SeoTitle.Length is < 20 or > 200 ||
                record.SeoDescription.Length is < 70 or > 500 ||
                queries.Any(query => query.Length > 200))
            {
                continue;
            }
            context.CollectionSuggestions.Add(record);
            existingSlugs.Add(slug);
            saved++;
        }
        await context.SaveChangesAsync(cancellationToken);
        var cost = aiOptions.EstimateCostUsd(output.InputTokens ?? 0, output.OutputTokens ?? 0);
        return new(true, products.Length, output.Suggestions.Count, saved, cost,
            $"Saved {saved} AI collection suggestion{(saved == 1 ? "" : "s")} as review-only drafts. No collection was created or published.");
    }

    public async Task<CollectionCommandResult> CreateCollectionDraftAsync(
        string shopSlug,
        Guid suggestionId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var suggestion = await context.CollectionSuggestions
            .Include(item => item.Shop)
            .SingleOrDefaultAsync(item => item.Id == suggestionId && item.Shop.Slug == shopSlug, cancellationToken);
        if (suggestion is null) return CollectionCommandResult.Failure("The collection suggestion was not found.");
        if (suggestion.Status != CollectionSuggestionStatus.Draft)
            return CollectionCommandResult.Failure("Only an open suggestion can create a collection draft.");

        var nextOrder = (await context.Collections.AsNoTracking()
            .Where(item => item.ShopId == suggestion.ShopId)
            .MaxAsync(item => (int?)item.DisplayOrder, cancellationToken) ?? 0) + 10;
        var result = await collectionService.SaveAsync(new CollectionUpdate(
            null,
            shopSlug,
            suggestion.Slug,
            suggestion.DisplayName,
            suggestion.ShortDescription,
            suggestion.IntroductoryCopy,
            suggestion.SeoTitle,
            suggestion.SeoDescription,
            ReadStringArray(suggestion.DiscoveryQueriesJson),
            nextOrder,
            12,
            false), cancellationToken);
        if (!result.Succeeded) return result;

        await using var update = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await update.CollectionSuggestions.SingleAsync(item => item.Id == suggestionId, cancellationToken);
        row.Status = CollectionSuggestionStatus.Accepted;
        row.ReviewedUtc = timeProvider.GetUtcNow();
        row.ReviewedBy = Truncate(Normalise(actor), 256);
        row.ReviewNote = "Created an unpublished collection draft after administrator review.";
        await update.SaveChangesAsync(cancellationToken);
        return CollectionCommandResult.Success("An unpublished collection draft was created. It still requires products and a separate publication action.", result.CollectionId);
    }

    public async Task<bool> RejectAsync(
        string shopSlug,
        Guid suggestionId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.CollectionSuggestions.SingleOrDefaultAsync(item =>
            item.Id == suggestionId && item.Shop.Slug == shopSlug && item.Status == CollectionSuggestionStatus.Draft,
            cancellationToken);
        if (row is null) return false;
        row.Status = CollectionSuggestionStatus.Rejected;
        row.ReviewedUtc = timeProvider.GetUtcNow();
        row.ReviewedBy = Truncate(Normalise(actor), 256);
        row.ReviewNote = "Rejected by an administrator.";
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    internal static string ComputeInputHash(
        string shopSlug,
        IReadOnlyList<string> existingCollections,
        IReadOnlyList<CollectionSuggestionEvidence> products,
        int maximumSuggestions)
    {
        var canonical = string.Join('\n', new[]
        {
            PromptVersion,
            shopSlug,
            maximumSuggestions.ToString(),
            string.Join('|', existingCollections.Order(StringComparer.OrdinalIgnoreCase)),
            string.Join('\n', products.OrderBy(item => item.ProductId).Select(item =>
                $"{item.ProductId}|{item.Title}|{item.FirstCategory}|{item.SecondCategory}"))
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static IReadOnlyList<string> ReadStringArray(string value)
    {
        try { return JsonSerializer.Deserialize<string[]>(value) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string[] CleanList(IReadOnlyList<string>? values) =>
        values?.Select(Normalise).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length <= 80 ? slug : slug[..80].TrimEnd('-');
    }

    private static string Normalise(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    private static string SafeMessage(string message) => Truncate(Normalise(message), 300);
}
