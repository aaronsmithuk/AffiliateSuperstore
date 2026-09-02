using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AutonomousPilotReportServiceTests
{
    [Fact]
    public async Task GetReportAsync_ReturnsEveryUtcDayAndSeparatesAutomaticShadowAndAiEvidence()
    {
        var now = new DateTimeOffset(2026, 9, 2, 16, 30, 0, TimeSpan.Zero);
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await using (var context = factory.CreateDbContext())
        {
            context.AutonomousCatalogueDecisions.AddRange(
                Decision(now.AddHours(-1), AutonomousCatalogueMode.Automatic, AutonomousCatalogueDecision.WouldPublish, AutonomousCatalogueAction.Published),
                Decision(now.AddDays(-1), AutonomousCatalogueMode.Automatic, AutonomousCatalogueDecision.Hold, AutonomousCatalogueAction.None),
                Decision(now.AddDays(-2), AutonomousCatalogueMode.Shadow, AutonomousCatalogueDecision.WouldPublish, AutonomousCatalogueAction.ShadowRecorded),
                Decision(now.AddDays(-8), AutonomousCatalogueMode.Automatic, AutonomousCatalogueDecision.WouldPublish, AutonomousCatalogueAction.Published));
            context.AiInvocations.AddRange(
                Invocation(now.AddHours(-2), AiInvocationStatus.Succeeded, estimatedCost: .0123m),
                Invocation(now.AddDays(-1), AiInvocationStatus.Failed, reservedCost: .02m, estimatedCost: .02m),
                Invocation(now.AddDays(-2), AiInvocationStatus.CacheHit),
                Invocation(now.AddDays(-2), AiInvocationStatus.BudgetBlocked));
            context.AutomationWorkItems.Add(new AutomationWorkItemRecord
            {
                Id = Guid.CreateVersion7(),
                Type = AutomationWorkType.AutonomousReview,
                Status = AutomationWorkStatus.Pending,
                IdempotencyKey = "pilot-pending",
                QueuedUtc = now.AddHours(-3),
                AvailableUtc = now.AddHours(-3)
            });
            await context.SaveChangesAsync();
        }

        var report = await new AutonomousPilotReportService(factory, new FixedTimeProvider(now)).GetReportAsync();

        Assert.Equal(7, report.Days.Count);
        Assert.Equal(new DateOnly(2026, 8, 27), report.Days[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 2), report.Days[^1].Date);
        Assert.Equal(2, report.AutomaticEvaluations);
        Assert.Equal(1, report.ShadowEvaluations);
        Assert.Equal(2, report.WouldPublish);
        Assert.Equal(1, report.Held);
        Assert.Equal(1, report.Published);
        Assert.Equal(2, report.ActiveObservationDays);
        Assert.Equal(2, report.AiCalls);
        Assert.Equal(1, report.AiFailures);
        Assert.Equal(1, report.BudgetBlocks);
        Assert.Equal(1, report.CacheHits);
        Assert.Equal(.0323m, report.AiSpendUsd);
        Assert.Equal(1, report.PendingReviews);
        Assert.True(report.HasWarning);
        Assert.False(report.HasCriticalFault);
        Assert.Equal(AutonomousPilotPromotionState.Blocked, report.Promotion.State);
    }

    [Fact]
    public async Task GetReportAsync_FlagsDeadLettersAndAutomaticallyPausedPoliciesAsCritical()
    {
        var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await using (var context = factory.CreateDbContext())
        {
            context.AutomationWorkItems.Add(new AutomationWorkItemRecord
            {
                Id = Guid.CreateVersion7(),
                Type = AutomationWorkType.AutonomousReview,
                Status = AutomationWorkStatus.DeadLetter,
                IdempotencyKey = "pilot-dead-letter",
                QueuedUtc = now.AddHours(-1),
                AvailableUtc = now.AddHours(-1),
                CompletedUtc = now
            });
            context.AutonomousCataloguePolicies.Add(new AutonomousCataloguePolicyRecord
            {
                ShopId = Guid.CreateVersion7(),
                Mode = AutonomousCatalogueMode.Shadow,
                UpdatedBy = "automatic safety: ai.consecutive-failures",
                CreatedUtc = now.AddDays(-1),
                UpdatedUtc = now
            });
            await context.SaveChangesAsync();
        }

        var report = await new AutonomousPilotReportService(factory, new FixedTimeProvider(now)).GetReportAsync();

        Assert.Equal(1, report.DeadLetterReviews);
        Assert.Equal(1, report.SafetyPausedPolicies);
        Assert.True(report.HasCriticalFault);
        Assert.False(report.HasWarning);
        Assert.Equal(AutonomousPilotPromotionState.Blocked, report.Promotion.State);
    }

    [Fact]
    public async Task GetReportAsync_OnlyRecommendsOwnerReviewAfterMinimumCleanEvidence()
    {
        var now = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await using (var context = factory.CreateDbContext())
        {
            context.AutonomousCataloguePolicies.Add(new AutonomousCataloguePolicyRecord
            {
                ShopId = Guid.CreateVersion7(),
                Mode = AutonomousCatalogueMode.Automatic,
                UpdatedBy = "owner@example.test",
                CreatedUtc = now.AddDays(-9),
                UpdatedUtc = now.AddDays(-9)
            });
            context.AutonomousCatalogueDecisions.Add(
                Decision(now.AddDays(-8), AutonomousCatalogueMode.Automatic, AutonomousCatalogueDecision.Hold, AutonomousCatalogueAction.None));
            context.AutonomousCatalogueDecisions.AddRange(Enumerable.Range(0, 14).Select(index =>
                Decision(
                    now.AddHours(-index * 10),
                    AutonomousCatalogueMode.Automatic,
                    index < 7 ? AutonomousCatalogueDecision.WouldPublish : AutonomousCatalogueDecision.Hold,
                    index < 7 ? AutonomousCatalogueAction.Published : AutonomousCatalogueAction.None)));
            await context.SaveChangesAsync();
        }

        var report = await new AutonomousPilotReportService(factory, new FixedTimeProvider(now)).GetReportAsync();

        Assert.Equal(14, report.AutomaticEvaluations);
        Assert.Equal(7, report.Published);
        Assert.Equal(1, report.AutomaticPolicies);
        Assert.Equal(now.AddDays(-8), report.FirstAutomaticEvaluationUtc);
        Assert.Equal(AutonomousPilotPromotionState.ReadyForOwnerReview, report.Promotion.State);
        Assert.Single(report.Promotion.Findings);
    }

    [Fact]
    public async Task GetReportAsync_ReportsNotStartedWithoutAutomaticEvidence()
    {
        var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        var report = await new AutonomousPilotReportService(
            new InMemoryFactory(Guid.NewGuid().ToString("N")),
            new FixedTimeProvider(now)).GetReportAsync();

        Assert.Equal(AutonomousPilotPromotionState.NotStarted, report.Promotion.State);
    }

    private static AutonomousCatalogueDecisionRecord Decision(
        DateTimeOffset evaluatedUtc,
        AutonomousCatalogueMode mode,
        AutonomousCatalogueDecision decision,
        AutonomousCatalogueAction action) => new()
        {
            Id = Guid.CreateVersion7(),
            ShopId = Guid.CreateVersion7(),
            ProductId = Guid.CreateVersion7().ToString("N"),
            EditorialVersionNumber = 1,
            Mode = mode,
            Decision = decision,
            Action = action,
            ReadinessScore = .99m,
            Summary = "test",
            EvaluatedUtc = evaluatedUtc
        };

    private static AiInvocationRecord Invocation(
        DateTimeOffset requestedUtc,
        AiInvocationStatus status,
        decimal reservedCost = 0m,
        decimal estimatedCost = 0m) => new()
        {
            Id = Guid.CreateVersion7(),
            Purpose = AiInvocationAuditService.ProductCopyPurpose,
            ProductId = Guid.CreateVersion7().ToString("N"),
            Provider = "test",
            Model = "test",
            PromptVersion = "test",
            InputHash = Guid.CreateVersion7().ToString("N"),
            CacheKey = Guid.CreateVersion7().ToString("N"),
            Status = status,
            RequestedUtc = requestedUtc,
            CompletedUtc = requestedUtc,
            ReservedCostUsd = reservedCost,
            EstimatedCostUsd = estimatedCost
        };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }
}
