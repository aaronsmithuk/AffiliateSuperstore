using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record AutonomousPilotDayReport(
    DateOnly Date,
    int AutomaticEvaluations,
    int ShadowEvaluations,
    int WouldPublish,
    int Held,
    int Published,
    int AiCalls,
    int AiFailures,
    int BudgetBlocks,
    decimal AiSpendUsd);

public enum AutonomousPilotPromotionState
{
    NotStarted,
    Observing,
    Blocked,
    ReadyForOwnerReview
}

public sealed record AutonomousPilotPromotionAssessment(
    AutonomousPilotPromotionState State,
    IReadOnlyList<string> Findings);

public sealed record AutonomousPilotReport(
    DateTimeOffset GeneratedUtc,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    DateTimeOffset? FirstAutomaticEvaluationUtc,
    IReadOnlyList<AutonomousPilotDayReport> Days,
    int AutomaticEvaluations,
    int ShadowEvaluations,
    int WouldPublish,
    int Held,
    int Published,
    int ActiveObservationDays,
    int AiCalls,
    int AiFailures,
    int BudgetBlocks,
    int CacheHits,
    decimal AiSpendUsd,
    int PendingReviews,
    int FailedReviews,
    int DeadLetterReviews,
    int SafetyPausedPolicies,
    int AutomaticPolicies)
{
    public bool HasCriticalFault => DeadLetterReviews > 0 || SafetyPausedPolicies > 0;
    public bool HasWarning => !HasCriticalFault &&
        (AutomaticEvaluations == 0 || FailedReviews > 0 || AiFailures > 0 || BudgetBlocks > 0);
    public AutonomousPilotPromotionAssessment Promotion => AutonomousPilotPromotionPolicy.Assess(this);
}

public static class AutonomousPilotPromotionPolicy
{
    public const int MinimumObservationDays = 7;
    public const int MinimumAutomaticEvaluations = 14;
    public const int MinimumAutomaticPublications = 7;

    public static AutonomousPilotPromotionAssessment Assess(AutonomousPilotReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var blockers = new List<string>();
        if (report.SafetyPausedPolicies > 0) blockers.Add("A policy was downgraded by the automatic safety circuit.");
        if (report.DeadLetterReviews > 0) blockers.Add("Autonomous review work reached the dead-letter queue.");
        if (report.FailedReviews > 0) blockers.Add("Autonomous review work was cancelled during the evidence window.");
        if (report.AiFailures > 0) blockers.Add("Product-copy AI calls failed during the evidence window.");
        if (report.BudgetBlocks > 0) blockers.Add("The AI budget blocked product-copy work during the evidence window.");
        if (blockers.Count > 0)
        {
            return new(AutonomousPilotPromotionState.Blocked, blockers);
        }

        if (report.FirstAutomaticEvaluationUtc is null)
        {
            return new(AutonomousPilotPromotionState.NotStarted,
                ["No automatic catalogue decision has been recorded yet."]);
        }

        if (report.AutomaticPolicies == 0)
        {
            return new(AutonomousPilotPromotionState.Blocked,
                ["No shop currently has an Automatic policy armed."]);
        }

        var observations = new List<string>();
        var observationAge = report.GeneratedUtc - report.FirstAutomaticEvaluationUtc.Value;
        if (observationAge < TimeSpan.FromDays(MinimumObservationDays))
        {
            var remaining = Math.Max(1, (int)Math.Ceiling((TimeSpan.FromDays(MinimumObservationDays) - observationAge).TotalDays));
            observations.Add($"Observe for approximately {remaining} more day(s) before reviewing the limits.");
        }
        if (report.AutomaticEvaluations < MinimumAutomaticEvaluations)
        {
            observations.Add($"Record at least {MinimumAutomaticEvaluations} automatic decisions in the rolling window ({report.AutomaticEvaluations} recorded).");
        }
        if (report.Published < MinimumAutomaticPublications)
        {
            observations.Add($"Review at least {MinimumAutomaticPublications} automatic publications in the rolling window ({report.Published} recorded).");
        }
        if (report.PendingReviews > 0)
        {
            observations.Add($"Let the autonomous review queue drain ({report.PendingReviews} pending or leased).");
        }

        return observations.Count > 0
            ? new(AutonomousPilotPromotionState.Observing, observations)
            : new(AutonomousPilotPromotionState.ReadyForOwnerReview,
                ["Seven days and the minimum evidence volume are complete with no recorded safety, queue, AI or budget faults."]);
    }
}

public sealed class AutonomousPilotReportService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    TimeProvider timeProvider)
{
    public async Task<AutonomousPilotReport> GetReportAsync(
        int dayCount = 7,
        CancellationToken cancellationToken = default)
    {
        dayCount = Math.Clamp(dayCount, 1, 31);
        var now = timeProvider.GetUtcNow();
        var windowEnd = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        var windowStart = windowEnd.AddDays(-dayCount);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var decisions = await context.AutonomousCatalogueDecisions.AsNoTracking()
            .Where(item => item.EvaluatedUtc >= windowStart && item.EvaluatedUtc < windowEnd)
            .Select(item => new DecisionSample(item.EvaluatedUtc, item.Mode, item.Decision, item.Action))
            .ToListAsync(cancellationToken);
        var firstAutomaticEvaluationUtc = await context.AutonomousCatalogueDecisions.AsNoTracking()
            .Where(item => item.Mode == AutonomousCatalogueMode.Automatic)
            .OrderBy(item => item.EvaluatedUtc)
            .Select(item => (DateTimeOffset?)item.EvaluatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var invocations = await context.AiInvocations.AsNoTracking()
            .Where(item => item.Purpose == AiInvocationAuditService.ProductCopyPurpose &&
                           item.RequestedUtc >= windowStart && item.RequestedUtc < windowEnd)
            .Select(item => new InvocationSample(
                item.RequestedUtc,
                item.Status,
                item.ReservedCostUsd,
                item.EstimatedCostUsd))
            .ToListAsync(cancellationToken);
        var reviews = await context.AutomationWorkItems.AsNoTracking()
            .Where(item => item.Type == AutomationWorkType.AutonomousReview &&
                           item.QueuedUtc >= windowStart && item.QueuedUtc < windowEnd)
            .Select(item => item.Status)
            .ToListAsync(cancellationToken);
        var policies = await context.AutonomousCataloguePolicies.AsNoTracking()
            .Select(item => new { item.Mode, item.UpdatedBy })
            .ToListAsync(cancellationToken);
        var safetyPausedPolicies = policies.Count(item =>
            item.Mode == AutonomousCatalogueMode.Shadow &&
            item.UpdatedBy.StartsWith("automatic safety:", StringComparison.OrdinalIgnoreCase));

        var days = Enumerable.Range(0, dayCount)
            .Select(offset => BuildDay(
                DateOnly.FromDateTime(windowStart.UtcDateTime.AddDays(offset)),
                decisions,
                invocations))
            .ToArray();

        return new AutonomousPilotReport(
            now,
            windowStart,
            windowEnd,
            firstAutomaticEvaluationUtc,
            days,
            days.Sum(item => item.AutomaticEvaluations),
            days.Sum(item => item.ShadowEvaluations),
            days.Sum(item => item.WouldPublish),
            days.Sum(item => item.Held),
            days.Sum(item => item.Published),
            days.Count(item => item.AutomaticEvaluations > 0),
            days.Sum(item => item.AiCalls),
            days.Sum(item => item.AiFailures),
            days.Sum(item => item.BudgetBlocks),
            invocations.Count(item => item.Status == AiInvocationStatus.CacheHit),
            days.Sum(item => item.AiSpendUsd),
            reviews.Count(item => item is AutomationWorkStatus.Pending or AutomationWorkStatus.Leased),
            reviews.Count(item => item == AutomationWorkStatus.Cancelled),
            reviews.Count(item => item == AutomationWorkStatus.DeadLetter),
            safetyPausedPolicies,
            policies.Count(item => item.Mode == AutonomousCatalogueMode.Automatic));
    }

    private static AutonomousPilotDayReport BuildDay(
        DateOnly date,
        IReadOnlyCollection<DecisionSample> decisions,
        IReadOnlyCollection<InvocationSample> invocations)
    {
        var dayDecisions = decisions.Where(item => DateOnly.FromDateTime(item.EvaluatedUtc.UtcDateTime) == date).ToArray();
        var dayInvocations = invocations.Where(item => DateOnly.FromDateTime(item.RequestedUtc.UtcDateTime) == date).ToArray();
        return new AutonomousPilotDayReport(
            date,
            dayDecisions.Count(item => item.Mode == AutonomousCatalogueMode.Automatic),
            dayDecisions.Count(item => item.Mode == AutonomousCatalogueMode.Shadow),
            dayDecisions.Count(item => item.Decision == AutonomousCatalogueDecision.WouldPublish),
            dayDecisions.Count(item => item.Decision == AutonomousCatalogueDecision.Hold),
            dayDecisions.Count(item => item.Action == AutonomousCatalogueAction.Published),
            dayInvocations.Count(item => item.Status is not AiInvocationStatus.CacheHit and not AiInvocationStatus.BudgetBlocked),
            dayInvocations.Count(item => item.Status == AiInvocationStatus.Failed),
            dayInvocations.Count(item => item.Status == AiInvocationStatus.BudgetBlocked),
            dayInvocations
                .Where(item => item.Status is not AiInvocationStatus.CacheHit and not AiInvocationStatus.BudgetBlocked)
                .Sum(item => item.Status == AiInvocationStatus.Reserved ? item.ReservedCostUsd : item.EstimatedCostUsd));
    }

    private sealed record DecisionSample(
        DateTimeOffset EvaluatedUtc,
        AutonomousCatalogueMode Mode,
        AutonomousCatalogueDecision Decision,
        AutonomousCatalogueAction Action);

    private sealed record InvocationSample(
        DateTimeOffset RequestedUtc,
        AiInvocationStatus Status,
        decimal ReservedCostUsd,
        decimal EstimatedCostUsd);
}
