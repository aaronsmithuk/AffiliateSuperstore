using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record ProductIdentityRebuildResult(
    int ProductsRead,
    int ProfilesUpdated,
    int CandidatesCreated,
    int CandidatesUpdated);

public sealed record ProductIdentityCommandResult(bool Succeeded, string Message);

public sealed class ProductIdentityService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    TimeProvider timeProvider)
{
    public const string NormalizerVersion = "1.4";
    public const string MatcherVersion = "1.5";
    private const int MaximumCandidatesPerProduct = 50;
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NonToken = new(@"[^\p{L}\p{N}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PackPattern = new(@"(?:pack|set)\s+of\s+(?<count>\d{1,3})|(?<count>\d{1,3})\s*(?:pcs?|pieces?|pack)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex SizePattern = new(@"(?<size>\d+(?:\.\d+)?)\s*(?<unit>mm|cm|inches?|inch|in)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex MultiSizePattern = new(@"\d+(?:\.\d+)?\s*(?:/|-)\s*\d+(?:\.\d+)?\s*(?:mm|cm|inches?|inch|in)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ModelPattern = new(@"\b(?=[a-z0-9-]{4,20}\b)(?=[a-z0-9-]*[a-z])(?=[a-z0-9-]*\d)[a-z0-9]+(?:-[a-z0-9]+)*\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex MeasurementTokenPattern = new(@"^\d+(?:\.\d+)?(?:mm|cm|inches?|inch|in)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "for", "from", "gift", "gifts", "hot", "new", "of", "on", "sale", "soft", "the", "toy", "toys", "with"
    };
    private static readonly string[] Colours = ["black", "blue", "brown", "cream", "gold", "green", "grey", "gray", "orange", "pink", "purple", "red", "silver", "white", "yellow"];
    private static readonly string[] Materials = ["cotton", "plush", "polyester", "velvet", "wool"];

    public async Task<ProductIdentityRebuildResult> RebuildAsync(
        string shopSlug,
        int maximumProducts = 500,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        if (maximumProducts is < 2 or > 5000) throw new ArgumentOutOfRangeException(nameof(maximumProducts));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var products = await context.ShopProducts
            .AsNoTracking()
            .Where(item => item.Shop.Slug == shopSlug && item.IsActive && item.Product.IsEligible)
            .Select(item => item.Product)
            .Distinct()
            .OrderByDescending(item => item.LastCheckedUtc)
            .Take(maximumProducts)
            .ToArrayAsync(cancellationToken);
        var ids = products.Select(item => item.AliExpressProductId).ToArray();
        var profiles = await context.ProductIdentityProfiles
            .Where(item => ids.Contains(item.ProductId))
            .ToDictionaryAsync(item => item.ProductId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var updated = 0;

        foreach (var product in products)
        {
            var normalized = Normalize(product, now);
            if (!profiles.TryGetValue(product.AliExpressProductId, out var existing))
            {
                context.ProductIdentityProfiles.Add(normalized);
                profiles.Add(normalized.ProductId, normalized);
                updated++;
            }
            else if (!string.Equals(existing.InputHash, normalized.InputHash, StringComparison.Ordinal) ||
                     !string.Equals(existing.NormalizerVersion, NormalizerVersion, StringComparison.Ordinal))
            {
                existing.NormalizedTitle = normalized.NormalizedTitle;
                existing.NormalizedGtin = normalized.NormalizedGtin;
                existing.NormalizedModel = normalized.NormalizedModel;
                existing.PackCount = normalized.PackCount;
                existing.SizeCentimetres = normalized.SizeCentimetres;
                existing.Colour = normalized.Colour;
                existing.Material = normalized.Material;
                existing.TokensJson = normalized.TokensJson;
                existing.InputHash = normalized.InputHash;
                existing.NormalizerVersion = NormalizerVersion;
                existing.UpdatedUtc = now;
                updated++;
            }
        }
        await context.SaveChangesAsync(cancellationToken);

        var productLookup = products.ToDictionary(item => item.AliExpressProductId, StringComparer.Ordinal);
        var scored = new List<ScoredCandidate>();
        var profileRows = profiles.Values.Where(item => ids.Contains(item.ProductId)).OrderBy(item => item.ProductId).ToArray();
        for (var leftIndex = 0; leftIndex < profileRows.Length; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < profileRows.Length; rightIndex++)
            {
                var left = profileRows[leftIndex];
                var right = profileRows[rightIndex];
                var match = Score(left, right, productLookup[left.ProductId], productLookup[right.ProductId]);
                if (match is not null) scored.Add(match);
            }
        }

        var pairCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var selected = scored.OrderByDescending(item => item.Confidence).Where(item =>
        {
            pairCounts.TryGetValue(item.LeftProductId, out var leftCount);
            pairCounts.TryGetValue(item.RightProductId, out var rightCount);
            if (leftCount >= MaximumCandidatesPerProduct || rightCount >= MaximumCandidatesPerProduct) return false;
            pairCounts[item.LeftProductId] = leftCount + 1;
            pairCounts[item.RightProductId] = rightCount + 1;
            return true;
        }).ToArray();
        var candidateRows = await context.ProductMatchCandidates
            .Where(item => ids.Contains(item.LeftProductId) && ids.Contains(item.RightProductId))
            .ToArrayAsync(cancellationToken);
        var existingCandidates = candidateRows
            .Where(item => item.MatcherVersion == MatcherVersion)
            .ToDictionary(item => item.LeftProductId + "\n" + item.RightProductId, StringComparer.Ordinal);
        var created = 0;
        var candidatesUpdated = 0;
        var selectedKeys = selected.Select(item => item.LeftProductId + "\n" + item.RightProductId).ToHashSet(StringComparer.Ordinal);
        foreach (var match in selected)
        {
            var key = match.LeftProductId + "\n" + match.RightProductId;
            if (!existingCandidates.TryGetValue(key, out var candidate))
            {
                context.ProductMatchCandidates.Add(ToRecord(match, now));
                created++;
            }
            else
            {
                var changed = !candidate.IsCurrent;
                candidate.IsCurrent = true;
                if (candidate.ReviewStatus == ProductMatchReviewStatus.Pending)
                {
                    candidate.SuggestedRelationship = match.Relationship;
                    candidate.Confidence = match.Confidence;
                    candidate.BlockingReason = match.BlockingReason;
                    candidate.EvidenceJson = match.EvidenceJson;
                    candidate.ConflictJson = match.ConflictJson;
                    candidate.GeneratedUtc = now;
                    changed = true;
                }
                if (changed) candidatesUpdated++;
            }
        }
        foreach (var candidate in candidateRows.Where(item => item.IsCurrent &&
                     (item.MatcherVersion != MatcherVersion || !selectedKeys.Contains(item.LeftProductId + "\n" + item.RightProductId))))
        {
            candidate.IsCurrent = false;
            candidatesUpdated++;
        }
        await context.SaveChangesAsync(cancellationToken);
        return new(products.Length, updated, created, candidatesUpdated);
    }

    public async Task<ProductIdentityCommandResult> ReviewAsync(
        Guid candidateId,
        bool accept,
        string reviewedBy,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var candidate = await context.ProductMatchCandidates
            .Include(item => item.LeftProduct)
            .Include(item => item.RightProduct)
            .SingleOrDefaultAsync(item => item.Id == candidateId, cancellationToken);
        if (candidate is null) return new(false, "The match candidate could not be found.");
        if (candidate.ReviewStatus != ProductMatchReviewStatus.Pending) return new(false, "This candidate has already been reviewed.");

        var now = timeProvider.GetUtcNow();
        if (accept)
        {
            var memberships = await context.CanonicalProductMembers
                .Where(item => item.ProductId == candidate.LeftProductId || item.ProductId == candidate.RightProductId)
                .ToArrayAsync(cancellationToken);
            var left = memberships.SingleOrDefault(item => item.ProductId == candidate.LeftProductId);
            var right = memberships.SingleOrDefault(item => item.ProductId == candidate.RightProductId);
            if (left is not null && right is not null && left.CanonicalProductId != right.CanonicalProductId)
            {
                return new(false, "The offers already belong to different canonical products; merging those groups requires a separate reviewed operation.");
            }

            var canonicalId = left?.CanonicalProductId ?? right?.CanonicalProductId;
            if (canonicalId is null)
            {
                canonicalId = Guid.CreateVersion7();
                context.CanonicalProducts.Add(new CanonicalProductRecord
                {
                    Id = canonicalId.Value,
                    DisplayName = candidate.LeftProduct.Title,
                    CreatedUtc = now,
                    UpdatedUtc = now
                });
                context.CanonicalProductMembers.Add(new CanonicalProductMemberRecord
                {
                    CanonicalProductId = canonicalId.Value,
                    ProductId = candidate.LeftProductId,
                    Relationship = ProductRelationship.Primary,
                    EvidenceCandidateId = candidate.Id,
                    LinkedUtc = now
                });
            }

            if (left is null && right is not null)
            {
                context.CanonicalProductMembers.Add(Member(canonicalId.Value, candidate.LeftProductId, candidate.SuggestedRelationship, candidate.Id, now));
            }
            else if (right is null)
            {
                context.CanonicalProductMembers.Add(Member(canonicalId.Value, candidate.RightProductId, candidate.SuggestedRelationship, candidate.Id, now));
            }
            candidate.ReviewStatus = ProductMatchReviewStatus.Accepted;
        }
        else
        {
            candidate.ReviewStatus = ProductMatchReviewStatus.Rejected;
        }

        candidate.ReviewedUtc = now;
        candidate.ReviewedBy = string.IsNullOrWhiteSpace(reviewedBy) ? "administrator" : reviewedBy.Trim();
        await context.SaveChangesAsync(cancellationToken);
        return new(true, accept ? "The reviewed relationship was linked without changing either source offer." : "The suggested relationship was rejected.");
    }

    private static ProductIdentityProfileRecord Normalize(ProductRecord product, DateTimeOffset now)
    {
        var titleWithSeparators = WebUtility.HtmlDecode(product.Title).Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var title = NormalizeText(product.Title);
        var tokens = Tokenize(title);
        var gtin = NormalizeGtin(product.EanCode);
        var sizes = SizePattern.Matches(title).Select(ParseCentimetres).Where(item => item is not null).Select(item => item!.Value).Distinct().ToArray();
        var hasMultipleSizeOptions = MultiSizePattern.IsMatch(titleWithSeparators);
        var input = JsonSerializer.Serialize(new { product.Title, product.EanCode, product.SkuId, product.SecondLevelCategoryId, Version = NormalizerVersion });
        return new ProductIdentityProfileRecord
        {
            ProductId = product.AliExpressProductId,
            NormalizedTitle = title,
            NormalizedGtin = gtin,
            NormalizedModel = ExtractModel(titleWithSeparators),
            PackCount = PackPattern.Match(title) is { Success: true } pack && int.TryParse(pack.Groups["count"].Value, out var count) ? count : null,
            SizeCentimetres = !hasMultipleSizeOptions && sizes.Length == 1 ? sizes[0] : null,
            Colour = Colours.FirstOrDefault(colour => tokens.Contains(colour)),
            Material = Materials.FirstOrDefault(material => tokens.Contains(material)),
            TokensJson = JsonSerializer.Serialize(tokens.OrderBy(item => item)),
            InputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))),
            NormalizerVersion = NormalizerVersion,
            UpdatedUtc = now
        };
    }

    private static ScoredCandidate? Score(ProductIdentityProfileRecord left, ProductIdentityProfileRecord right, ProductRecord leftProduct, ProductRecord rightProduct)
    {
        var exactGtin = left.NormalizedGtin is not null && left.NormalizedGtin == right.NormalizedGtin;
        var sameCategory = !string.IsNullOrWhiteSpace(leftProduct.SecondLevelCategoryId) && leftProduct.SecondLevelCategoryId == rightProduct.SecondLevelCategoryId;
        if (!exactGtin && !sameCategory) return null;
        var leftTokens = ReadTokens(left.TokensJson);
        var rightTokens = ReadTokens(right.TokensJson);
        var intersection = leftTokens.Intersect(rightTokens).Count();
        var union = leftTokens.Union(rightTokens).Count();
        var tokenScore = union == 0 ? 0m : (decimal)intersection / union;
        var sameModel = left.NormalizedModel is not null && left.NormalizedModel == right.NormalizedModel;
        if (!exactGtin && !sameModel && tokenScore < .55m) return null;
        var conflicts = new List<string>();
        ProductRelationship relationship;
        decimal confidence;

        if (exactGtin)
        {
            relationship = ProductRelationship.Duplicate;
            confidence = .995m;
        }
        else if (left.PackCount is not null && right.PackCount is not null && left.PackCount != right.PackCount)
        {
            relationship = ProductRelationship.Bundle;
            confidence = Math.Max(.90m, .78m + tokenScore * .18m);
            conflicts.Add($"pack count differs ({left.PackCount} vs {right.PackCount})");
        }
        else if (MeaningfulSizeConflict(left.SizeCentimetres, right.SizeCentimetres))
        {
            relationship = ProductRelationship.Variant;
            confidence = Math.Max(.90m, .76m + tokenScore * .18m);
            conflicts.Add($"size differs ({left.SizeCentimetres:0.##} cm vs {right.SizeCentimetres:0.##} cm)");
        }
        else if (left.NormalizedModel is not null && right.NormalizedModel is not null && left.NormalizedModel != right.NormalizedModel)
        {
            relationship = ProductRelationship.Variant;
            confidence = Math.Max(.90m, .76m + tokenScore * .18m);
            conflicts.Add($"model differs ({left.NormalizedModel} vs {right.NormalizedModel})");
        }
        else if (left.Colour is not null && right.Colour is not null && left.Colour != right.Colour && tokenScore >= .55m)
        {
            relationship = ProductRelationship.Variant;
            confidence = .90m;
            conflicts.Add($"colour differs ({left.Colour} vs {right.Colour})");
        }
        else
        {
            var attributeScore = Same(left.PackCount, right.PackCount) * .08m +
                                 Same(left.SizeCentimetres, right.SizeCentimetres) * .08m +
                                 Same(left.Colour, right.Colour) * .05m +
                                 Same(left.Material, right.Material) * .04m;
            confidence = Math.Min(.98m, .55m + tokenScore * .35m + attributeScore);
            if (confidence < .75m) return null;
            relationship = ProductRelationship.Duplicate;
        }

        var evidence = JsonSerializer.Serialize(new
        {
            ExactGtin = exactGtin,
            SameCategory = sameCategory,
            SameModel = sameModel,
            TokenJaccard = tokenScore,
            left.PackCount,
            RightPackCount = right.PackCount,
            left.SizeCentimetres,
            RightSizeCentimetres = right.SizeCentimetres,
            LeftColour = left.Colour,
            RightColour = right.Colour,
            LeftMaterial = left.Material,
            RightMaterial = right.Material
        });
        return new ScoredCandidate(
            left.ProductId,
            right.ProductId,
            relationship,
            confidence,
            exactGtin ? "exact valid GTIN" : "same AliExpress category with deterministic title/attribute evidence",
            evidence,
            conflicts.Count == 0 ? null : JsonSerializer.Serialize(conflicts));
    }

    private static ProductMatchCandidateRecord ToRecord(ScoredCandidate match, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        LeftProductId = match.LeftProductId,
        RightProductId = match.RightProductId,
        SuggestedRelationship = match.Relationship,
        Confidence = match.Confidence,
        BlockingReason = match.BlockingReason,
        EvidenceJson = match.EvidenceJson,
        ConflictJson = match.ConflictJson,
        MatcherVersion = MatcherVersion,
        GeneratedUtc = now
    };

    private static CanonicalProductMemberRecord Member(Guid canonicalId, string productId, ProductRelationship relationship, Guid evidenceId, DateTimeOffset now) => new()
    {
        CanonicalProductId = canonicalId,
        ProductId = productId,
        Relationship = relationship,
        EvidenceCandidateId = evidenceId,
        LinkedUtc = now
    };

    private static string NormalizeText(string value)
    {
        var decoded = WebUtility.HtmlDecode(value).Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        return Whitespace.Replace(NonToken.Replace(decoded, " "), " ").Trim();
    }

    private static HashSet<string> Tokenize(string value) => value
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(token => token.Length > 1 && !StopWords.Contains(token))
        .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> ReadTokens(string json)
    {
        try { return (JsonSerializer.Deserialize<string[]>(json) ?? []).ToHashSet(StringComparer.Ordinal); }
        catch (JsonException) { return []; }
    }

    private static string? NormalizeGtin(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is not (8 or 12 or 13 or 14)) return null;
        var sum = 0;
        var weight = 3;
        for (var index = digits.Length - 2; index >= 0; index--)
        {
            sum += (digits[index] - '0') * weight;
            weight = weight == 3 ? 1 : 3;
        }
        var check = (10 - sum % 10) % 10;
        return check == digits[^1] - '0' ? digits : null;
    }

    private static string? ExtractModel(string title) => ModelPattern.Matches(title)
        .Select(match => match.Value)
        .FirstOrDefault(value => !MeasurementTokenPattern.IsMatch(value));

    private static decimal? ParseCentimetres(Match match)
    {
        if (!decimal.TryParse(match.Groups["size"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)) return null;
        return match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "mm" => value / 10,
            "in" or "inch" or "inches" => value * 2.54m,
            _ => value
        };
    }

    private static bool MeaningfulSizeConflict(decimal? left, decimal? right) =>
        left is not null && right is not null &&
        Math.Abs(left.Value - right.Value) / Math.Max(left.Value, right.Value) > .15m;

    private static decimal Same<T>(T? left, T? right) where T : struct =>
        left is not null && right is not null && EqualityComparer<T>.Default.Equals(left.Value, right.Value) ? 1 : 0;

    private static decimal Same(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && string.Equals(left, right, StringComparison.Ordinal) ? 1 : 0;

    private sealed record ScoredCandidate(
        string LeftProductId,
        string RightProductId,
        ProductRelationship Relationship,
        decimal Confidence,
        string BlockingReason,
        string EvidenceJson,
        string? ConflictJson);
}
