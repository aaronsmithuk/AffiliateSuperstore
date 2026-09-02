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

public sealed record AutonomousPilotReport(
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
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
    int SafetyPausedPolicies)
{
    public bool HasCriticalFault => DeadLetterReviews > 0 || SafetyPausedPolicies > 0;
    public bool HasWarning => !HasCriticalFault &&
        (AutomaticEvaluations == 0 || FailedReviews > 0 || AiFailures > 0 || BudgetBlocks > 0);
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
        var safetyPausedPolicies = await context.AutonomousCataloguePolicies.AsNoTracking()
            .CountAsync(item => item.Mode == AutonomousCatalogueMode.Shadow &&
                                item.UpdatedBy.StartsWith("automatic safety:"), cancellationToken);

        var days = Enumerable.Range(0, dayCount)
            .Select(offset => BuildDay(
                DateOnly.FromDateTime(windowStart.UtcDateTime.AddDays(offset)),
                decisions,
                invocations))
            .ToArray();

        return new AutonomousPilotReport(
            windowStart,
            windowEnd,
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
            safetyPausedPolicies);
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
