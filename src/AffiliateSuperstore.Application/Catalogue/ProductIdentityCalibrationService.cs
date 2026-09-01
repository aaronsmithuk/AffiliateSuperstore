using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record ProductIdentityGoldLabelCommandResult(bool Succeeded, string Message);

public sealed record ProductIdentityCalibrationReport(
    string MatcherVersion,
    ProductIdentityGoldSetSlice? Slice,
    int CandidatePairs,
    int IndividualLabels,
    int EffectivePairs,
    int SingleReviewedPairs,
    int DoubleReviewedPairs,
    int AgreementPairs,
    int DisagreementPairs,
    int AdjudicatedPairs,
    int IdentityPositivePairs,
    decimal? CandidatePrecision,
    decimal? RelationshipAccuracy,
    IReadOnlyList<ProductIdentityThresholdCalibration> Thresholds);

public sealed record ProductIdentityThresholdCalibration(
    decimal Threshold,
    int EvaluatedPairs,
    int IdentityPositivePairs,
    decimal? QueuePrecision,
    int ExactRelationshipPairs,
    decimal? RelationshipAccuracy,
    int AutoLinkEvaluatedPairs,
    int AutoLinkCorrectPairs,
    int FalseMergePairs,
    decimal? AutoLinkPrecision,
    decimal? AutoLinkWilsonLowerBound);

public sealed class ProductIdentityCalibrationService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    TimeProvider timeProvider)
{
    public const int IdentityGoldSetTarget = 500;
    private static readonly decimal[] Thresholds = [.75m, .80m, .85m, .90m, .95m, .97m, .985m, .99m];

    public async Task<ProductIdentityGoldLabelCommandResult> AddLabelAsync(
        Guid candidateId,
        ProductRelationship label,
        ProductIdentityGoldSetSlice slice,
        string rationale,
        string reviewedBy,
        bool isAdjudication = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reviewedBy)) return new(false, "A named reviewer is required.");
        rationale = rationale?.Trim() ?? string.Empty;
        if (rationale.Length < 10) return new(false, "Add a short evidence-based rationale of at least 10 characters.");
        if (rationale.Length > 1000) return new(false, "The rationale must be 1,000 characters or fewer.");

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var candidate = await context.ProductMatchCandidates
            .Include(item => item.GoldLabels)
            .SingleOrDefaultAsync(item => item.Id == candidateId, cancellationToken);
        if (candidate is null) return new(false, "The match candidate could not be found.");

        var existingSlices = candidate.GoldLabels.Select(item => item.Slice).Distinct().ToArray();
        if (existingSlices.Length > 0 && existingSlices.Any(item => item != slice))
        {
            return new(false, $"This pair is already assigned to the {existingSlices[0]} slice. Gold-set pairs cannot move between evaluation slices.");
        }

        var reviewer = reviewedBy.Trim();
        if (isAdjudication)
        {
            var latestReviewerLabels = candidate.GoldLabels
                .Where(item => !item.IsAdjudication)
                .GroupBy(item => item.Reviewer, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.CreatedUtc).First().Label)
                .Distinct()
                .ToArray();
            if (latestReviewerLabels.Length < 2)
            {
                return new(false, "Adjudication is only available after two reviewers have recorded different labels.");
            }
        }

        var latestByReviewer = candidate.GoldLabels
            .Where(item => item.IsAdjudication == isAdjudication &&
                           string.Equals(item.Reviewer, reviewer, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CreatedUtc)
            .FirstOrDefault();
        if (latestByReviewer is not null && latestByReviewer.Label == label &&
            string.Equals(latestByReviewer.Rationale, rationale, StringComparison.Ordinal))
        {
            return new(false, "That reviewer label and rationale are already the current entry for this pair.");
        }

        context.ProductIdentityGoldLabels.Add(new ProductIdentityGoldLabelRecord
        {
            Id = Guid.CreateVersion7(),
            CandidateId = candidateId,
            Label = label,
            Slice = slice,
            Reviewer = reviewer,
            Rationale = rationale,
            IsAdjudication = isAdjudication,
            CreatedUtc = timeProvider.GetUtcNow()
        });
        await context.SaveChangesAsync(cancellationToken);
        return new(true, isAdjudication
            ? "The disagreement was adjudicated. The original reviewer labels remain in the audit history."
            : "The gold-set label was recorded without changing canonical membership or the public catalogue.");
    }

    public async Task<ProductIdentityCalibrationReport> BuildReportAsync(
        string shopSlug,
        string? matcherVersion = null,
        ProductIdentityGoldSetSlice? slice = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        matcherVersion = string.IsNullOrWhiteSpace(matcherVersion) ? ProductIdentityService.MatcherVersion : matcherVersion.Trim();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var shopProductIds = context.ShopProducts.AsNoTracking()
            .Where(item => item.Shop.Slug == shopSlug && item.IsActive)
            .Select(item => item.ProductId);
        var candidates = await context.ProductMatchCandidates.AsNoTracking()
            .Where(item => item.MatcherVersion == matcherVersion &&
                           shopProductIds.Contains(item.LeftProductId) &&
                           shopProductIds.Contains(item.RightProductId))
            .Include(item => item.GoldLabels)
            .ToArrayAsync(cancellationToken);

        var evaluations = new List<EvaluatedPair>();
        var individualLabels = 0;
        var singleReviewed = 0;
        var doubleReviewed = 0;
        var agreements = 0;
        var disagreements = 0;
        var adjudicated = 0;

        foreach (var candidate in candidates)
        {
            var labels = candidate.GoldLabels.Where(item => slice is null || item.Slice == slice).ToArray();
            individualLabels += labels.Length;
            if (labels.Length == 0) continue;

            var adjudication = labels.Where(item => item.IsAdjudication)
                .OrderByDescending(item => item.CreatedUtc)
                .FirstOrDefault();
            var reviewerLabels = labels.Where(item => !item.IsAdjudication)
                .GroupBy(item => item.Reviewer, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.CreatedUtc).First())
                .ToArray();

            if (reviewerLabels.Length == 1) singleReviewed++;
            if (reviewerLabels.Length >= 2) doubleReviewed++;
            var distinct = reviewerLabels.Select(item => item.Label).Distinct().ToArray();
            if (reviewerLabels.Length >= 2 && distinct.Length == 1) agreements++;
            if (reviewerLabels.Length >= 2 && distinct.Length > 1) disagreements++;

            ProductRelationship? effectiveLabel = null;
            if (adjudication is not null)
            {
                effectiveLabel = adjudication.Label;
                adjudicated++;
            }
            else if (distinct.Length == 1)
            {
                effectiveLabel = distinct[0];
            }

            if (effectiveLabel is not null)
            {
                evaluations.Add(new EvaluatedPair(
                    candidate.SuggestedRelationship,
                    effectiveLabel.Value,
                    candidate.Confidence,
                    string.IsNullOrWhiteSpace(candidate.ConflictJson)));
            }
        }

        var identityPositive = evaluations.Count(item => IsIdentityRelationship(item.GoldLabel));
        var exactRelationships = evaluations.Count(item => item.SuggestedRelationship == item.GoldLabel);
        var rows = Thresholds.Select(threshold => CalculateThreshold(evaluations, threshold)).ToArray();
        return new ProductIdentityCalibrationReport(
            matcherVersion,
            slice,
            candidates.Length,
            individualLabels,
            evaluations.Count,
            singleReviewed,
            doubleReviewed,
            agreements,
            disagreements,
            adjudicated,
            identityPositive,
            Ratio(identityPositive, evaluations.Count),
            Ratio(exactRelationships, evaluations.Count),
            rows);
    }

    private static ProductIdentityThresholdCalibration CalculateThreshold(IReadOnlyCollection<EvaluatedPair> evaluations, decimal threshold)
    {
        var selected = evaluations.Where(item => item.Confidence >= threshold).ToArray();
        var identityPositive = selected.Count(item => IsIdentityRelationship(item.GoldLabel));
        var exact = selected.Count(item => item.SuggestedRelationship == item.GoldLabel);
        var autoLink = selected.Where(item => item.HasNoHardConflict &&
            item.SuggestedRelationship is ProductRelationship.Duplicate or ProductRelationship.Translation).ToArray();
        var autoCorrect = autoLink.Count(item => item.GoldLabel == item.SuggestedRelationship);
        var falseMerges = autoLink.Length - autoCorrect;
        return new ProductIdentityThresholdCalibration(
            threshold,
            selected.Length,
            identityPositive,
            Ratio(identityPositive, selected.Length),
            exact,
            Ratio(exact, selected.Length),
            autoLink.Length,
            autoCorrect,
            falseMerges,
            Ratio(autoCorrect, autoLink.Length),
            WilsonLowerBound(autoCorrect, autoLink.Length));
    }

    private static bool IsIdentityRelationship(ProductRelationship relationship) => relationship is
        ProductRelationship.Duplicate or ProductRelationship.Translation or ProductRelationship.Variant or ProductRelationship.Bundle;

    private static decimal? Ratio(int numerator, int denominator) => denominator == 0
        ? null
        : decimal.Round((decimal)numerator / denominator, 5);

    internal static decimal? WilsonLowerBound(int successes, int total)
    {
        if (total == 0) return null;
        const double z = 1.959963984540054;
        var n = (double)total;
        var p = successes / n;
        var denominator = 1 + z * z / n;
        var centre = p + z * z / (2 * n);
        var margin = z * Math.Sqrt(p * (1 - p) / n + z * z / (4 * n * n));
        return decimal.Round((decimal)((centre - margin) / denominator), 5);
    }

    private sealed record EvaluatedPair(
        ProductRelationship SuggestedRelationship,
        ProductRelationship GoldLabel,
        decimal Confidence,
        bool HasNoHardConflict);
}
