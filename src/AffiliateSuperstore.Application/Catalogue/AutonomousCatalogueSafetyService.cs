using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record AutonomousCatalogueSafetyResult(
    bool Blocked,
    AutonomousCatalogueMode EffectiveMode,
    string? ReasonCode,
    string Message);

public sealed class AutonomousCatalogueSafetyService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    AutonomousCatalogueOptions options,
    TimeProvider timeProvider)
{
    private const string SafetyActorPrefix = "automatic safety:";

    public async Task<AutonomousCatalogueSafetyResult> EnsureSafeAsync(
        AutonomousCataloguePolicy policy,
        Guid? currentWorkItemId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (!options.AutomaticSafetyCircuitEnabled || policy.Mode != AutonomousCatalogueMode.Automatic)
        {
            return new(false, policy.Mode, null, "The automatic safety circuit did not need to intervene.");
        }

        var now = timeProvider.GetUtcNow();
        var lookback = now.AddHours(-Math.Clamp(options.AutomaticPauseLookbackHours, 1, 168));
        var acknowledgedAfter = policy.UpdatedUtc > lookback ? policy.UpdatedUtc : lookback;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var hasRecentDeadLetter = await context.AutomationWorkItems.AsNoTracking().AnyAsync(item =>
            item.ShopId == policy.ShopId &&
            item.Type == AutomationWorkType.AutonomousReview &&
            item.Status == AutomationWorkStatus.DeadLetter &&
            item.CompletedUtc >= acknowledgedAfter &&
            (!currentWorkItemId.HasValue || item.Id != currentWorkItemId.Value), cancellationToken);
        if (hasRecentDeadLetter)
        {
            return await PauseAsync(
                policy,
                "autonomous.dead-letter",
                "Automatic publication was paused because an autonomous review reached the dead-letter queue. Review the failure, then save Automatic mode to acknowledge and re-arm it.",
                now,
                cancellationToken);
        }

        var failureLimit = Math.Clamp(options.AutomaticPauseConsecutiveFailedAiCalls, 2, 10);
        var recentStatuses = await context.AiInvocations.AsNoTracking()
            .Where(item =>
                item.Purpose == AiInvocationAuditService.ProductCopyPurpose &&
                item.RequestedUtc >= acknowledgedAfter &&
                item.Status != AiInvocationStatus.CacheHit &&
                item.Status != AiInvocationStatus.BudgetBlocked &&
                context.ShopProducts.Any(shopProduct =>
                    shopProduct.ShopId == policy.ShopId &&
                    shopProduct.ProductId == item.ProductId))
            .OrderByDescending(item => item.RequestedUtc)
            .Take(failureLimit)
            .Select(item => item.Status)
            .ToArrayAsync(cancellationToken);
        if (recentStatuses.Length == failureLimit && recentStatuses.All(status => status == AiInvocationStatus.Failed))
        {
            return await PauseAsync(
                policy,
                "ai.consecutive-failures",
                $"Automatic publication was paused after {failureLimit} consecutive product-copy AI failures. Check provider health and the invocation audit, then save Automatic mode to acknowledge and re-arm it.",
                now,
                cancellationToken);
        }

        return new(false, policy.Mode, null, "No automatic-publication safety condition was detected.");
    }

    public static bool IsAutomaticSafetyPause(AutonomousCataloguePolicy policy) =>
        policy.Mode == AutonomousCatalogueMode.Shadow &&
        policy.UpdatedBy.StartsWith(SafetyActorPrefix, StringComparison.OrdinalIgnoreCase);

    private async Task<AutonomousCatalogueSafetyResult> PauseAsync(
        AutonomousCataloguePolicy policy,
        string reasonCode,
        string message,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.AutonomousCataloguePolicies.SingleAsync(
            item => item.ShopId == policy.ShopId,
            cancellationToken);
        if (row.Mode == AutonomousCatalogueMode.Automatic)
        {
            row.Mode = AutonomousCatalogueMode.Shadow;
            row.UpdatedUtc = now;
            row.UpdatedBy = $"{SafetyActorPrefix} {reasonCode}";
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return new(true, AutonomousCatalogueMode.Shadow, reasonCode,
                    "A safety condition was detected and this publication cycle was stopped while the policy changed concurrently. Reload the policy before re-arming automatic mode.");
            }
        }

        return new(true, row.Mode, reasonCode, message);
    }
}
