using System.Text.Json;
using System.Text.RegularExpressions;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed class CatalogueCollectionService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    CatalogueSeoPolicy seoPolicy,
    TimeProvider timeProvider)
{
    public const int MaximumDiscoveryQueries = 8;
    public const int MinimumIndexingTarget = 8;
    public const int MaximumIndexingTarget = 48;

    private static readonly string[] RestrictedBrandTerms =
    [
        "aliexpress", "ali express", "nintendo", "pokemon", "pokémon", "mario", "zelda",
        "minecraft", "sonic", "kirby", "disney", "marvel", "star wars", "sanrio",
        "hello kitty", "squishmallow", "squishmallows", "barbie"
    ];

    public static IReadOnlyList<RecommendedCollectionDefinition> RecommendedPlushCollections { get; } =
    [
        new(
            "animal-friends",
            "Animal Friends",
            "Cows, rabbits, pigs, bears and other familiar soft companions.",
            "Browse a friendly mix of farmyard, woodland and household animal plushies. We favour clear listings with useful size information, current seller feedback and plenty of personality.",
            "Animal Plush Toys & Soft Companions",
            "Browse curated animal plush toys including cows, rabbits, pigs, bears and woodland friends.",
            ["animal plush toy", "woodland animal plush", "farm animal plush toy"],
            10),
        new(
            "ocean-friends",
            "Ocean & River Friends",
            "Seals, otters, whales, sharks, axolotls and other water-loving characters.",
            "Meet plush creatures inspired by oceans, rivers and rock pools. Check the stated dimensions and available options on the seller page, as photographs can make smaller plushies look deceptively large.",
            "Ocean Animal Plush Toys",
            "Discover curated seal, otter, whale, shark and axolotl plush toys from independent marketplace sellers.",
            ["seal plush toy", "otter plush toy", "axolotl plush toy", "whale plush toy"],
            20),
        new(
            "weird-wonderful",
            "Weird & Wonderful",
            "Capybaras, frogs and delightfully unusual plush personalities.",
            "This is the home of the charmingly unexpected: expressive frogs, laid-back capybaras and unusual animals that stand out from an ordinary teddy-bear shelf.",
            "Unusual & Funny Plush Toys",
            "Explore unusual plush toys including capybaras, frogs and wonderfully odd animal companions.",
            ["capybara plush toy", "frog plush toy", "unusual animal plush toy"],
            30),
        new(
            "fantasy-friends",
            "Fantasy & Prehistoric",
            "Original dragons, dinosaurs, unicorns and friendly imaginary creatures.",
            "Travel from prehistoric playrooms to imaginary kingdoms with creature-led designs that do not rely on a named entertainment franchise. Every product still passes the normal image and intellectual-property review before publication.",
            "Dragon, Dinosaur & Fantasy Plush Toys",
            "Browse original dragon, dinosaur, unicorn and fantasy creature plush toys without franchise-led merchandising.",
            ["dragon plush toy", "dinosaur plush toy", "unicorn plush toy", "cute monster plush"],
            40),
        new(
            "gamer-favourites",
            "Gamer Favourites",
            "Controller, pixel and arcade-inspired plushies without franchise branding.",
            "A playful collection for gaming spaces, built around generic controllers, pixels and arcade motifs. Recognisable protected characters are excluded unless genuine licensing can be established.",
            "Gaming-Inspired Plush Toys & Cushions",
            "Browse generic gaming-inspired plushies, controller cushions and pixel-style designs without implied brand affiliation.",
            ["game controller plush", "pixel game plush toy", "arcade plush cushion"],
            50),
        new(
            "cute-food",
            "Cute Food & Novelty",
            "Fruit, snacks, drinks and food-animal mash-ups with soft edges.",
            "Food-shaped plushies bring a little humour to desks, sofas and gift bags. Look for option-specific measurements and compare the final seller-page price before ordering.",
            "Cute Food Plush Toys & Novelty Cushions",
            "Discover fruit, snack, dessert and drink-shaped plush toys and novelty cushions.",
            ["cute food plush", "fruit plush toy", "dessert plush pillow", "drink plush toy"],
            60),
        new(
            "plush-cushions",
            "Plush Cushions",
            "Pillow-shaped and long-form plushies for beds, sofas and reading corners.",
            "These designs sit somewhere between a soft toy and a decorative cushion. Measurements, filling and fabric can vary by option, so use our shortlist as a starting point and confirm the details with the seller.",
            "Plush Cushions & Soft Animal Pillows",
            "Browse curated plush cushions, long animal pillows and soft decorative companions.",
            ["plush cushion", "long plush pillow", "animal plush pillow"],
            70),
        new(
            "mini-plush",
            "Minis & Bag Charms",
            "Small plush keyrings, bag charms and pocket-sized companions.",
            "A compact collection for bags, keys and small gifts. Product imagery can exaggerate scale, so we prioritise listings with clear measurements and ask shoppers to confirm the chosen option before checkout.",
            "Mini Plush Keyrings & Bag Charms",
            "Browse mini plush toys, soft keyrings, bag charms and pocket-sized companions.",
            ["mini plush keychain", "plush bag charm", "small plush pendant"],
            80)
    ];

    public async Task<IReadOnlyList<CollectionAdminSummary>> GetCollectionsAsync(
        string shopSlug,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var collections = await context.Collections.AsNoTracking()
            .Where(item => item.Shop.Slug == shopSlug)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.DisplayName)
            .ToListAsync(cancellationToken);

        if (collections.Count == 0) return [];
        var collectionIds = collections.Select(item => item.Id).ToArray();
        var candidates = await (
            from membership in context.CollectionProducts.AsNoTracking()
            join collection in context.Collections.AsNoTracking() on membership.CollectionId equals collection.Id
            join shopProduct in context.ShopProducts.AsNoTracking()
                on new { collection.ShopId, membership.ProductId }
                equals new { shopProduct.ShopId, shopProduct.ProductId }
            where collectionIds.Contains(collection.Id)
            select new CollectionProductSeoCandidate(
                collection.Id,
                shopProduct.IsActive &&
                    shopProduct.ReviewStatus == ProductReviewStatus.Approved &&
                    shopProduct.Product.IsEligible &&
                    shopProduct.Product.AffiliateLinks.Any(link =>
                        link.ShopId == shopProduct.ShopId && link.Status == AffiliateLinkStatus.Active),
                shopProduct.EditorialTitle,
                shopProduct.EditorialDescription,
                shopProduct.Product.MainImageUrl,
                shopProduct.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc)
                    .Select(snapshot => snapshot.SalePrice).FirstOrDefault(),
                shopProduct.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc)
                    .Select(snapshot => snapshot.FetchedUtc).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return collections.Select(collection =>
        {
            var assigned = candidates.Where(item => item.CollectionId == collection.Id).ToArray();
            var publicProducts = assigned.Where(item => item.IsPublic).ToArray();
            var indexable = publicProducts.Count(IsIndexable);
            return new CollectionAdminSummary(
                collection.Id,
                collection.Slug,
                collection.DisplayName,
                collection.ShortDescription,
                collection.IntroductoryCopy,
                collection.SeoTitle,
                collection.SeoDescription,
                ReadQueries(collection.DiscoveryQueriesJson),
                collection.DisplayOrder,
                collection.MinimumProductsForIndexing,
                collection.IsFeatured,
                collection.IsPublished,
                assigned.Length,
                publicProducts.Length,
                indexable,
                indexable >= collection.MinimumProductsForIndexing,
                collection.RowVersion);
        }).ToArray();
    }

    public async Task<IReadOnlyList<CollectionProductCandidate>> GetProductCandidatesAsync(
        string shopSlug,
        Guid collectionId,
        string? search = null,
        int maximumResults = 250,
        CancellationToken cancellationToken = default)
    {
        maximumResults = Math.Clamp(maximumResults, 1, 500);
        var normalisedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var collection = await context.Collections.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == collectionId && item.Shop.Slug == shopSlug, cancellationToken);
        if (collection is null) return [];

        var query = context.ShopProducts.AsNoTracking()
            .Where(item => item.ShopId == collection.ShopId && item.IsActive && item.Product.IsEligible);
        if (normalisedSearch is not null)
        {
            query = query.Where(item =>
                item.Product.Title.Contains(normalisedSearch) ||
                (item.EditorialTitle != null && item.EditorialTitle.Contains(normalisedSearch)));
        }

        return await query
            .OrderBy(item => item.ReviewStatus == ProductReviewStatus.Approved ? 0 : 1)
            .ThenByDescending(item => item.IsFeatured)
            .ThenByDescending(item => item.Product.Snapshots.Max(snapshot => snapshot.RecentSalesVolume))
            .Take(maximumResults)
            .Select(item => new CollectionProductCandidate(
                item.ProductId,
                item.EditorialTitle ?? item.Product.Title,
                item.Product.MainImageUrl,
                item.ReviewStatus,
                item.Product.SecondLevelCategoryName,
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc)
                    .Select(snapshot => snapshot.SalePrice).FirstOrDefault(),
                item.Product.Snapshots.OrderByDescending(snapshot => snapshot.FetchedUtc)
                    .Select(snapshot => snapshot.Currency).FirstOrDefault(),
                item.Product.Collections.Any(membership => membership.CollectionId == collectionId),
                item.Product.Collections.Where(membership => membership.CollectionId == collectionId)
                    .Select(membership => membership.IsFeatured).FirstOrDefault(),
                item.Product.Collections.Where(membership => membership.CollectionId == collectionId)
                    .Select(membership => membership.DisplayOrder).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<CollectionCommandResult> SeedRecommendedAsync(
        string shopSlug,
        string actor,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var shop = await context.Shops.SingleOrDefaultAsync(item => item.Slug == shopSlug, cancellationToken);
        if (shop is null) return CollectionCommandResult.Failure($"Shop '{shopSlug}' was not found.");

        var existing = await context.Collections.Where(item => item.ShopId == shop.Id)
            .Select(item => item.Slug).ToListAsync(cancellationToken);
        var existingSlugs = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = timeProvider.GetUtcNow();
        var added = 0;
        foreach (var definition in RecommendedPlushCollections.Where(item => !existingSlugs.Contains(item.Slug)))
        {
            context.Collections.Add(new CollectionRecord
            {
                Id = Guid.CreateVersion7(),
                ShopId = shop.Id,
                Slug = definition.Slug,
                DisplayName = definition.DisplayName,
                ShortDescription = definition.ShortDescription,
                IntroductoryCopy = definition.IntroductoryCopy,
                SeoTitle = definition.SeoTitle,
                SeoDescription = definition.SeoDescription,
                DiscoveryQueriesJson = JsonSerializer.Serialize(definition.DiscoveryQueries),
                DisplayOrder = definition.DisplayOrder,
                MinimumProductsForIndexing = 12,
                IsFeatured = definition.DisplayOrder <= 40,
                IsPublished = false,
                CreatedUtc = now,
                UpdatedUtc = now
            });
            added++;
        }

        await context.SaveChangesAsync(cancellationToken);
        return CollectionCommandResult.Success(
            added == 0
                ? "The recommended collections already exist."
                : $"Created {added} draft collections. Nothing was published automatically.");
    }

    public async Task<CollectionCommandResult> SaveAsync(
        CollectionUpdate update,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(update);
        if (errors.Count > 0) return CollectionCommandResult.Failure("Collection validation failed.", errors);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var shop = await context.Shops.SingleOrDefaultAsync(item => item.Slug == update.ShopSlug, cancellationToken);
        if (shop is null) return CollectionCommandResult.Failure($"Shop '{update.ShopSlug}' was not found.");

        var slug = NormaliseSlug(update.Slug);
        var duplicate = await context.Collections.AnyAsync(item =>
            item.ShopId == shop.Id && item.Slug == slug && item.Id != update.CollectionId, cancellationToken);
        if (duplicate) return CollectionCommandResult.Failure($"A collection using '/{slug}' already exists.");

        var now = timeProvider.GetUtcNow();
        var record = update.CollectionId is null
            ? null
            : await context.Collections.SingleOrDefaultAsync(item =>
                item.Id == update.CollectionId && item.ShopId == shop.Id, cancellationToken);
        if (update.CollectionId is not null && record is null)
        {
            return CollectionCommandResult.Failure("The collection no longer exists.");
        }
        if (record is null)
        {
            record = new CollectionRecord
            {
                Id = Guid.CreateVersion7(),
                ShopId = shop.Id,
                CreatedUtc = now,
                IsPublished = false
            };
            context.Collections.Add(record);
        }

        record.Slug = slug;
        record.DisplayName = update.DisplayName.Trim();
        record.ShortDescription = update.ShortDescription.Trim();
        record.IntroductoryCopy = update.IntroductoryCopy.Trim();
        record.SeoTitle = update.SeoTitle.Trim();
        record.SeoDescription = update.SeoDescription.Trim();
        record.DiscoveryQueriesJson = JsonSerializer.Serialize(NormaliseQueries(update.DiscoveryQueries));
        record.DisplayOrder = update.DisplayOrder;
        record.MinimumProductsForIndexing = update.MinimumProductsForIndexing;
        record.IsFeatured = update.IsFeatured;
        record.UpdatedUtc = now;
        await context.SaveChangesAsync(cancellationToken);
        return CollectionCommandResult.Success("Collection draft saved.", record.Id);
    }

    public async Task<CollectionCommandResult> SetPublicationAsync(
        string shopSlug,
        Guid collectionId,
        bool publish,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var collection = await context.Collections.SingleOrDefaultAsync(item =>
            item.Id == collectionId && item.Shop.Slug == shopSlug, cancellationToken);
        if (collection is null) return CollectionCommandResult.Failure("The collection was not found.");
        if (collection.IsPublished == publish)
        {
            return CollectionCommandResult.Success(publish ? "Collection is already published." : "Collection is already a draft.", collection.Id);
        }

        if (publish)
        {
            var summaries = await GetCollectionsAsync(shopSlug, cancellationToken);
            var summary = summaries.Single(item => item.Id == collectionId);
            if (!summary.CanPublish)
            {
                return CollectionCommandResult.Failure(
                    $"This collection needs {Math.Max(0, summary.MinimumProductsForIndexing - summary.IndexableProducts)} more indexable products before publication.");
            }
        }

        collection.IsPublished = publish;
        collection.UpdatedUtc = timeProvider.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
        return CollectionCommandResult.Success(publish ? "Collection published." : "Collection returned to draft.", collection.Id);
    }

    public async Task<CollectionCommandResult> SetMembershipAsync(
        string shopSlug,
        Guid collectionId,
        string productId,
        bool assigned,
        bool featured,
        int displayOrder,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (displayOrder is < 0 or > 10000)
        {
            return CollectionCommandResult.Failure("Display order must be between 0 and 10,000.");
        }
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var collection = await context.Collections.SingleOrDefaultAsync(item =>
            item.Id == collectionId && item.Shop.Slug == shopSlug, cancellationToken);
        if (collection is null) return CollectionCommandResult.Failure("The collection was not found.");
        var belongsToShop = await context.ShopProducts.AnyAsync(item =>
            item.ShopId == collection.ShopId && item.ProductId == productId && item.IsActive && item.Product.IsEligible,
            cancellationToken);
        if (!belongsToShop) return CollectionCommandResult.Failure("This product is not an active, eligible product in the selected shop.");

        var membership = await context.CollectionProducts.SingleOrDefaultAsync(item =>
            item.CollectionId == collectionId && item.ProductId == productId, cancellationToken);
        if (!assigned)
        {
            if (membership is not null) context.CollectionProducts.Remove(membership);
            await context.SaveChangesAsync(cancellationToken);
            return CollectionCommandResult.Success("Product removed from the collection.", collectionId);
        }

        if (membership is null)
        {
            membership = new CollectionProductRecord
            {
                CollectionId = collectionId,
                ProductId = productId,
                AssignedUtc = timeProvider.GetUtcNow(),
                AssignedBy = string.IsNullOrWhiteSpace(actor) ? "administrator" : actor.Trim()
            };
            context.CollectionProducts.Add(membership);
        }
        membership.IsFeatured = featured;
        membership.DisplayOrder = displayOrder;
        await context.SaveChangesAsync(cancellationToken);
        return CollectionCommandResult.Success("Collection membership saved.", collectionId);
    }

    public static IReadOnlyList<string> ReadQueries(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<string[]>(json)
                ?.Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToArray() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private bool IsIndexable(CollectionProductSeoCandidate candidate) =>
        candidate.IsPublic && seoPolicy.IsProductIndexable(
            candidate.EditorialTitle,
            candidate.EditorialDescription,
            candidate.ImageUrl,
            candidate.Price,
            candidate.LastCheckedUtc);

    private static List<string> Validate(CollectionUpdate update)
    {
        var errors = new List<string>();
        var slug = NormaliseSlug(update.Slug);
        if (!Regex.IsMatch(slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant) || slug.Length > 80)
        {
            errors.Add("Slug must contain only lower-case letters, numbers and single hyphens.");
        }
        CheckLength(update.DisplayName, 3, 160, "Display name", errors);
        CheckLength(update.ShortDescription, 30, 500, "Short description", errors);
        CheckLength(update.IntroductoryCopy, 120, 4000, "Introductory copy", errors);
        CheckLength(update.SeoTitle, 20, 200, "SEO title", errors);
        CheckLength(update.SeoDescription, 70, 500, "SEO description", errors);
        if (update.DisplayOrder is < 0 or > 10000) errors.Add("Display order must be between 0 and 10,000.");
        if (update.MinimumProductsForIndexing is < MinimumIndexingTarget or > MaximumIndexingTarget)
        {
            errors.Add($"Indexing target must be between {MinimumIndexingTarget} and {MaximumIndexingTarget} products.");
        }
        var queries = NormaliseQueries(update.DiscoveryQueries);
        if (queries.Count == 0) errors.Add("Add at least one generic discovery query.");
        if (queries.Count > MaximumDiscoveryQueries) errors.Add($"Use no more than {MaximumDiscoveryQueries} discovery queries.");
        if (queries.Any(item => item.Length > 200)) errors.Add("Discovery queries cannot exceed 200 characters.");
        var textToCheck = string.Join(' ', new[] { update.DisplayName, update.SeoTitle }.Concat(queries));
        var restricted = RestrictedBrandTerms.FirstOrDefault(term =>
            textToCheck.Contains(term, StringComparison.OrdinalIgnoreCase));
        if (restricted is not null)
        {
            errors.Add($"'{restricted}' is a restricted brand or character term. Use a generic collection concept and review individual products for licensing evidence.");
        }
        return errors;
    }

    private static void CheckLength(string? value, int minimum, int maximum, string field, ICollection<string> errors)
    {
        var length = value?.Trim().Length ?? 0;
        if (length < minimum || length > maximum) errors.Add($"{field} must contain {minimum}–{maximum} characters.");
    }

    private static string NormaliseSlug(string? value) =>
        (value ?? string.Empty).Trim().Trim('/').ToLowerInvariant();

    private static IReadOnlyList<string> NormaliseQueries(IEnumerable<string>? queries) =>
        (queries ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private sealed record CollectionProductSeoCandidate(
        Guid CollectionId,
        bool IsPublic,
        string? EditorialTitle,
        string? EditorialDescription,
        string? ImageUrl,
        decimal? Price,
        DateTimeOffset LastCheckedUtc);
}

public sealed record RecommendedCollectionDefinition(
    string Slug,
    string DisplayName,
    string ShortDescription,
    string IntroductoryCopy,
    string SeoTitle,
    string SeoDescription,
    IReadOnlyList<string> DiscoveryQueries,
    int DisplayOrder);

public sealed record CollectionAdminSummary(
    Guid Id,
    string Slug,
    string DisplayName,
    string ShortDescription,
    string IntroductoryCopy,
    string SeoTitle,
    string SeoDescription,
    IReadOnlyList<string> DiscoveryQueries,
    int DisplayOrder,
    int MinimumProductsForIndexing,
    bool IsFeatured,
    bool IsPublished,
    int AssignedProducts,
    int PublicProducts,
    int IndexableProducts,
    bool CanPublish,
    byte[] RowVersion);

public sealed record CollectionProductCandidate(
    string ProductId,
    string Title,
    string? ImageUrl,
    ProductReviewStatus ReviewStatus,
    string? SourceCategory,
    decimal? Price,
    string? Currency,
    bool IsAssigned,
    bool IsFeatured,
    int DisplayOrder);

public sealed record CollectionUpdate(
    Guid? CollectionId,
    string ShopSlug,
    string Slug,
    string DisplayName,
    string ShortDescription,
    string IntroductoryCopy,
    string SeoTitle,
    string SeoDescription,
    IReadOnlyList<string> DiscoveryQueries,
    int DisplayOrder,
    int MinimumProductsForIndexing,
    bool IsFeatured);

public sealed record CollectionCommandResult(
    bool Succeeded,
    string Message,
    Guid? CollectionId = null,
    IReadOnlyList<string>? Errors = null)
{
    public static CollectionCommandResult Success(string message, Guid? collectionId = null) =>
        new(true, message, collectionId, []);

    public static CollectionCommandResult Failure(string message, IReadOnlyList<string>? errors = null) =>
        new(false, message, null, errors ?? []);
}
