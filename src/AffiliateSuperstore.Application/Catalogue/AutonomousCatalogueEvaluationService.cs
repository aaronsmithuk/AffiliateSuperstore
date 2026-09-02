using System.Text.Json;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record AutonomousCatalogueCandidateEvidence(
    DateTimeOffset? SourceCheckedUtc,
    bool HasPublishedCollection,
    bool HasSourceChangeAfterDraft,
    decimal? DuplicateCandidateConfidence,
    bool IsKnownDuplicate,
    Guid? EditorialVersionId);

public sealed record AutonomousCatalogueAssessment(
    AutonomousCatalogueDecision Decision,
    decimal ReadinessScore,
    IReadOnlyList<string> ReasonCodes,
    string Summary);

public sealed record AutonomousCatalogueEvaluationResult(
    string ShopSlug,
    AutonomousCatalogueMode Mode,
    int DraftsPrepared,
    int ProductsEvaluated,
    int WouldPublish,
    int Held,
    int Published,
    decimal AiCostUsd,
    string Message);

public static class AutonomousCatalogueDecisionEngine
{
    public static AutonomousCatalogueAssessment Assess(
        CatalogueAiReviewItem item,
        AutonomousCatalogueCandidateEvidence evidence,
        AutonomousCataloguePolicy policy,
        DateTimeOffset now,
        int sourceStaleAfterHours)
    {
        var reasons = new List<string>();
        if (!item.IsActive) reasons.Add("product.inactive");
        if (!item.IsEligible) reasons.Add("product.ineligible");
        if (item.AvailabilityState != ProductAvailabilityState.Available) reasons.Add("product.unavailable");
        if (evidence.SourceCheckedUtc is null || evidence.SourceCheckedUtc < now.AddHours(-sourceStaleAfterHours)) reasons.Add("source.stale");
        if (string.IsNullOrWhiteSpace(item.ProductDetailUrl)) reasons.Add("product-url.missing");
        if (string.IsNullOrWhiteSpace(item.ImageUrl)) reasons.Add("media.missing");
        if (!item.HasActiveLink) reasons.Add("affiliate-link.missing");
        if (!evidence.HasPublishedCollection) reasons.Add("published-collection.missing");
        if (item.SalePrice is null or <= 0m) reasons.Add("price.missing");
        if (item.QualityFlags.Count > 0) reasons.Add("quality.flags");
        if (item.ValidationState != EditorialValidationState.Passed || item.ValidationFindings.Count > 0) reasons.Add("editorial.validation");
        if (item.WasHumanEdited) reasons.Add("editorial.human-edited");
        if (evidence.EditorialVersionId is null) reasons.Add("editorial.version-missing");
        if (evidence.HasSourceChangeAfterDraft) reasons.Add("editorial.source-changed");
        if (item.Invocation is null ||
            item.Invocation.Status is not (AiInvocationStatus.Succeeded or AiInvocationStatus.CacheHit) ||
            item.Invocation.ValidationState != EditorialValidationState.Passed ||
            !string.Equals(item.Invocation.PromptVersion, CatalogueAiSuggestionService.PromptVersion, StringComparison.Ordinal))
        {
            reasons.Add("ai.provenance");
        }
        if (evidence.IsKnownDuplicate) reasons.Add("duplicate.confirmed");
        if (evidence.DuplicateCandidateConfidence >= policy.DuplicateHoldConfidence) reasons.Add("duplicate.probable");

        var score = Math.Max(0m, 1m - reasons.Count * .10m);
        if (reasons.Count == 0 && score >= policy.MinimumReadinessScore)
        {
            return new(AutonomousCatalogueDecision.WouldPublish, score, [],
                "All deterministic publication gates passed; automatic mode would publish this product.");
        }

        if (reasons.Count == 0) reasons.Add("readiness.below-threshold");
        return new(AutonomousCatalogueDecision.Hold, score, reasons,
            $"Held for review: {string.Join(", ", reasons)}.");
    }
}

public sealed class AutonomousCatalogueEvaluationService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    AutonomousCataloguePolicyService policyService,
    CatalogueAiQueuePreparationService queuePreparationService,
    CatalogueAiReviewService reviewService,
    CatalogueEditorialService editorialService,
    AutonomousCatalogueOptions autonomousOptions,
    CatalogueAutomationOptions automationOptions,
    AiAutomationOptions aiOptions,
    TimeProvider timeProvider)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<AutonomousCatalogueEvaluationResult> RunAsync(
        string shopSlug,
        Guid? workItemId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        var policy = await policyService.GetAsync(shopSlug, cancellationToken)
            ?? throw new InvalidOperationException($"Autonomous policy for shop '{shopSlug}' was not found.");
        if (!autonomousOptions.Enabled || policy.Mode == AutonomousCatalogueMode.Off)
        {
            return Empty(shopSlug, policy.Mode, "Autonomous catalogue evaluation is off for this shop.");
        }
        if (!await Gate.WaitAsync(0, cancellationToken))
        {
            return Empty(shopSlug, policy.Mode, "Another autonomous catalogue evaluation is already running in this application instance.");
        }

        try
        {
            var now = timeProvider.GetUtcNow();
            var spentToday = await AiSpendTodayAsync(now, cancellationToken);
            var remainingBudget = Math.Max(0m, policy.DailyAiBudgetUsd - spentToday);
            var affordableCalls = aiOptions.MaximumReservedCostPerCallUsd <= 0m
                ? policy.MaximumCandidatesPerRun
                : (int)Math.Floor(remainingBudget / aiOptions.MaximumReservedCostPerCallUsd);
            var preparationCount = Math.Clamp(
                Math.Min(policy.MaximumCandidatesPerRun, affordableCalls),
                0,
                CatalogueAiQueuePreparationService.MaximumBatchSize);
            CatalogueAiQueuePreparationResult? preparation = null;
            if (preparationCount > 0)
            {
                preparation = await queuePreparationService.RunAsync(
                    shopSlug,
                    preparationCount,
                    $"autonomous {policy.Mode.ToString().ToLowerInvariant()}",
                    cancellationToken);
            }

            var dashboard = await reviewService.GetAsync(shopSlug, cancellationToken);
            var awaiting = dashboard.Items.Where(item => item.IsAwaitingReview).ToArray();
            if (awaiting.Length == 0)
            {
                return new(shopSlug, policy.Mode, preparation?.DraftsSaved ?? 0, 0, 0, 0, 0,
                    preparation?.EstimatedCostUsd ?? 0m,
                    preparationCount == 0 && policy.DailyAiBudgetUsd > 0m
                        ? "The daily AI budget is exhausted and no existing AI drafts are awaiting evaluation."
                        : "No AI-assisted drafts are awaiting autonomous evaluation.");
            }

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var productIds = awaiting.Select(item => item.ProductId).Distinct().ToArray();
            var shopId = policy.ShopId;
            var sourceChecks = await context.Products.AsNoTracking()
                .Where(item => productIds.Contains(item.AliExpressProductId))
                .ToDictionaryAsync(item => item.AliExpressProductId, item => item.LastCheckedUtc, cancellationToken);
            var productsWithPublishedCollections = (await context.CollectionProducts.AsNoTracking()
                .Where(item => productIds.Contains(item.ProductId) &&
                    item.Collection.ShopId == shopId &&
                    item.Collection.IsPublished)
                .Select(item => item.ProductId)
                .Distinct()
                .ToArrayAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
            var sourceChanges = await context.ProductChangeEvents.AsNoTracking()
                .Where(item => productIds.Contains(item.ProductId) &&
                    (item.Kind == ProductChangeEventKind.ContentChanged ||
                     item.Kind == ProductChangeEventKind.AvailabilityChanged))
                .Select(item => new { item.ProductId, item.OccurredUtc })
                .ToArrayAsync(cancellationToken);
            var duplicateRows = await context.ProductMatchCandidates.AsNoTracking()
                .Where(item => item.IsCurrent && item.ReviewStatus == ProductMatchReviewStatus.Pending &&
                    item.SuggestedRelationship == ProductRelationship.Duplicate &&
                    (productIds.Contains(item.LeftProductId) || productIds.Contains(item.RightProductId)))
                .Select(item => new { item.LeftProductId, item.RightProductId, item.Confidence })
                .ToArrayAsync(cancellationToken);
            var duplicateConfidence = productIds.ToDictionary(
                productId => productId,
                productId => duplicateRows
                    .Where(item => item.LeftProductId == productId || item.RightProductId == productId)
                    .Select(item => (decimal?)item.Confidence)
                    .Max());
            var knownDuplicates = (await context.CanonicalProductMembers.AsNoTracking()
                .Where(item => productIds.Contains(item.ProductId) && item.Relationship == ProductRelationship.Duplicate)
                .Select(item => item.ProductId)
                .ToArrayAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
            var versionRows = await context.EditorialVersions.AsNoTracking()
                .Where(item => item.ShopId == shopId && productIds.Contains(item.ProductId))
                .Select(item => new { item.Id, item.ProductId, item.VersionNumber })
                .ToArrayAsync(cancellationToken);
            var decisionRows = await context.AutonomousCatalogueDecisions.AsNoTracking()
                .Where(item => item.ShopId == shopId && productIds.Contains(item.ProductId))
                .OrderByDescending(item => item.EvaluatedUtc)
                .Select(item => new { item.ProductId, item.EditorialVersionNumber, item.EvaluatedUtc })
                .ToArrayAsync(cancellationToken);
            var latestDecision = decisionRows
                .GroupBy(item => (item.ProductId, item.EditorialVersionNumber))
                .ToDictionary(group => group.Key, group => group.Max(item => item.EvaluatedUtc));

            var selected = awaiting
                .OrderBy(item => latestDecision.ContainsKey((item.ProductId, item.CurrentVersionNumber)) ? 1 : 0)
                .ThenBy(item => latestDecision.GetValueOrDefault((item.ProductId, item.CurrentVersionNumber)))
                .ThenByDescending(item => item.AiCreatedUtc)
                .Take(policy.MaximumCandidatesPerRun)
                .ToArray();
            var publishedToday = await context.AutonomousCatalogueDecisions.CountAsync(item =>
                item.ShopId == shopId &&
                item.Action == AutonomousCatalogueAction.Published &&
                item.EvaluatedUtc >= StartOfUtcDay(now), cancellationToken);
            var records = new List<AutonomousCatalogueDecisionRecord>(selected.Length);

            foreach (var item in selected)
            {
                var versionId = versionRows
                    .Where(version => version.ProductId == item.ProductId && version.VersionNumber == item.CurrentVersionNumber)
                    .Select(version => (Guid?)version.Id)
                    .SingleOrDefault();
                var evidence = new AutonomousCatalogueCandidateEvidence(
                    sourceChecks.GetValueOrDefault(item.ProductId),
                    productsWithPublishedCollections.Contains(item.ProductId),
                    sourceChanges.Any(change => change.ProductId == item.ProductId && change.OccurredUtc > item.AiCreatedUtc),
                    duplicateConfidence.GetValueOrDefault(item.ProductId),
                    knownDuplicates.Contains(item.ProductId),
                    versionId);
                var assessment = AutonomousCatalogueDecisionEngine.Assess(
                    item,
                    evidence,
                    policy,
                    now,
                    automationOptions.ProductStaleAfterHours);
                var action = policy.Mode == AutonomousCatalogueMode.Shadow || !autonomousOptions.AutomaticPublishingEnabled
                    ? AutonomousCatalogueAction.ShadowRecorded
                    : AutonomousCatalogueAction.None;

                if (policy.Mode == AutonomousCatalogueMode.Automatic &&
                    autonomousOptions.AutomaticPublishingEnabled &&
                    assessment.Decision == AutonomousCatalogueDecision.WouldPublish)
                {
                    if (publishedToday >= policy.MaximumAutoPublishesPerDay)
                    {
                        assessment = assessment with
                        {
                            Decision = AutonomousCatalogueDecision.Hold,
                            ReasonCodes = ["publication.daily-limit"],
                            Summary = "Held because the shop's daily automatic-publication limit has been reached."
                        };
                    }
                    else
                    {
                        var publish = await editorialService.SetReviewStatusAsync(
                            shopSlug,
                            item.ProductId,
                            ProductReviewStatus.Approved,
                            cancellationToken);
                        if (publish.Succeeded)
                        {
                            action = AutonomousCatalogueAction.Published;
                            publishedToday++;
                        }
                        else
                        {
                            assessment = assessment with
                            {
                                Decision = AutonomousCatalogueDecision.Hold,
                                ReasonCodes = ["publication.final-gate"],
                                Summary = $"The final publication gate held this product: {publish.Message}"
                            };
                        }
                    }
                }

                records.Add(ToRecord(policy, item, evidence, assessment, action, workItemId, now));
            }

            context.AutonomousCatalogueDecisions.AddRange(records);
            await context.SaveChangesAsync(cancellationToken);
            var wouldPublish = records.Count(item => item.Decision == AutonomousCatalogueDecision.WouldPublish);
            var published = records.Count(item => item.Action == AutonomousCatalogueAction.Published);
            return new(
                shopSlug,
                policy.Mode,
                preparation?.DraftsSaved ?? 0,
                records.Count,
                wouldPublish,
                records.Count - wouldPublish,
                published,
                preparation?.EstimatedCostUsd ?? 0m,
                policy.Mode == AutonomousCatalogueMode.Shadow || !autonomousOptions.AutomaticPublishingEnabled
                    ? $"Shadow evaluation recorded {wouldPublish} would-publish and {records.Count - wouldPublish} hold decisions. Nothing was published."
                    : $"Automatic evaluation published {published} products and held {records.Count - published}.");
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<decimal> AiSpendTodayAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.AiInvocations.AsNoTracking()
            .Where(item => item.RequestedUtc >= StartOfUtcDay(now))
            .SumAsync(item => item.EstimatedCostUsd, cancellationToken);
    }

    private static AutonomousCatalogueDecisionRecord ToRecord(
        AutonomousCataloguePolicy policy,
        CatalogueAiReviewItem item,
        AutonomousCatalogueCandidateEvidence evidence,
        AutonomousCatalogueAssessment assessment,
        AutonomousCatalogueAction action,
        Guid? workItemId,
        DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        ShopId = policy.ShopId,
        ProductId = item.ProductId,
        WorkItemId = workItemId,
        EditorialVersionId = evidence.EditorialVersionId,
        EditorialVersionNumber = item.CurrentVersionNumber,
        Mode = policy.Mode,
        Decision = assessment.Decision,
        Action = action,
        ReadinessScore = assessment.ReadinessScore,
        ReasonCodesJson = JsonSerializer.Serialize(assessment.ReasonCodes),
        Summary = assessment.Summary,
        EvidenceJson = JsonSerializer.Serialize(new
        {
            item.IsActive,
            item.IsEligible,
            item.AvailabilityState,
            item.HasActiveLink,
            CollectionCount = item.Collections.Count,
            item.SalePrice,
            item.Currency,
            item.ValidationState,
            QualityFlags = item.QualityFlags.Select(flag => flag.Code),
            InvocationId = item.Invocation?.Id,
            InvocationStatus = item.Invocation?.Status,
            evidence.SourceCheckedUtc,
            evidence.HasPublishedCollection,
            evidence.HasSourceChangeAfterDraft,
            evidence.DuplicateCandidateConfidence,
            evidence.IsKnownDuplicate
        }),
        PolicySnapshotJson = JsonSerializer.Serialize(new
        {
            policy.Mode,
            policy.ReviewEveryHours,
            policy.MaximumCandidatesPerRun,
            policy.MaximumAutoPublishesPerDay,
            policy.MinimumReadinessScore,
            policy.DuplicateHoldConfidence,
            policy.DailyAiBudgetUsd
        }),
        EvaluatedUtc = now
    };

    private static DateTimeOffset StartOfUtcDay(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, 0, 0, 0, TimeSpan.Zero);

    private static AutonomousCatalogueEvaluationResult Empty(
        string shopSlug,
        AutonomousCatalogueMode mode,
        string message) => new(shopSlug, mode, 0, 0, 0, 0, 0, 0m, message);
}
