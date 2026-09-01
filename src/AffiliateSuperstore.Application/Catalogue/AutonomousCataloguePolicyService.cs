using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record AutonomousCataloguePolicy(
    Guid ShopId,
    string ShopSlug,
    string ShopName,
    AutonomousCatalogueMode Mode,
    int ReviewEveryHours,
    int MaximumCandidatesPerRun,
    int MaximumAutoPublishesPerDay,
    decimal MinimumReadinessScore,
    decimal DuplicateHoldConfidence,
    decimal DailyAiBudgetUsd,
    DateTimeOffset UpdatedUtc,
    string UpdatedBy,
    string ExpectedRowVersion);

public sealed record AutonomousCataloguePolicyUpdate(
    string ShopSlug,
    AutonomousCatalogueMode Mode,
    int ReviewEveryHours,
    int MaximumCandidatesPerRun,
    int MaximumAutoPublishesPerDay,
    decimal MinimumReadinessScore,
    decimal DuplicateHoldConfidence,
    decimal DailyAiBudgetUsd,
    string UpdatedBy,
    string? ExpectedRowVersion);

public sealed record AutonomousCataloguePolicyCommandResult(
    bool Succeeded,
    string Message,
    AutonomousCataloguePolicy? Policy = null);

public sealed class AutonomousCataloguePolicyService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    AutonomousCatalogueOptions defaults,
    TimeProvider timeProvider)
{
    public async Task<int> EnsureDefaultsAsync(CancellationToken cancellationToken = default)
    {
        if (!defaults.Enabled) return 0;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.AutonomousCataloguePolicies.AsNoTracking()
            .Select(item => item.ShopId)
            .ToArrayAsync(cancellationToken);
        var missing = await context.Shops.AsNoTracking()
            .Where(item => item.IsEnabled && !existing.Contains(item.Id))
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        if (missing.Length == 0) return 0;

        var now = timeProvider.GetUtcNow();
        context.AutonomousCataloguePolicies.AddRange(missing.Select(shopId => CreateDefault(shopId, now)));
        await context.SaveChangesAsync(cancellationToken);
        return missing.Length;
    }

    public async Task<IReadOnlyList<AutonomousCataloguePolicy>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.AutonomousCataloguePolicies.AsNoTracking()
            .Include(item => item.Shop)
            .Where(item => item.Shop.IsEnabled)
            .OrderBy(item => item.Shop.DisplayName)
            .ToListAsync(cancellationToken);
        return rows.Select(ToPolicy).ToArray();
    }

    public async Task<AutonomousCataloguePolicy?> GetAsync(
        string shopSlug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        await EnsureDefaultsAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.AutonomousCataloguePolicies.AsNoTracking()
            .Include(item => item.Shop)
            .SingleOrDefaultAsync(item => item.Shop.Slug == shopSlug && item.Shop.IsEnabled, cancellationToken);
        return row is null ? null : ToPolicy(row);
    }

    public async Task<AutonomousCataloguePolicyCommandResult> UpdateAsync(
        AutonomousCataloguePolicyUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        var error = Validate(update);
        if (error is not null) return new(false, error);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.AutonomousCataloguePolicies
            .Include(item => item.Shop)
            .SingleOrDefaultAsync(item => item.Shop.Slug == update.ShopSlug && item.Shop.IsEnabled, cancellationToken);
        if (row is null) return new(false, "The autonomous policy could not be found for this shop.");
        if (!MatchesRowVersion(row.RowVersion, update.ExpectedRowVersion))
        {
            return new(false, "This policy changed after it was loaded. Refresh before saving so another administrator's changes are not overwritten.");
        }

        row.Mode = update.Mode;
        row.ReviewEveryHours = update.ReviewEveryHours;
        row.MaximumCandidatesPerRun = update.MaximumCandidatesPerRun;
        row.MaximumAutoPublishesPerDay = update.MaximumAutoPublishesPerDay;
        row.MinimumReadinessScore = update.MinimumReadinessScore;
        row.DuplicateHoldConfidence = update.DuplicateHoldConfidence;
        row.DailyAiBudgetUsd = update.DailyAiBudgetUsd;
        row.UpdatedUtc = timeProvider.GetUtcNow();
        row.UpdatedBy = NormaliseActor(update.UpdatedBy);
        await context.SaveChangesAsync(cancellationToken);
        return new(true, update.Mode switch
        {
            AutonomousCatalogueMode.Off => "Autonomous evaluation is off for this shop.",
            AutonomousCatalogueMode.Shadow => "Shadow mode is active. Decisions will be recorded without publishing products.",
            _ => "Restricted automatic mode is active. Eligible products may be published within the configured limits."
        }, ToPolicy(row));
    }

    private AutonomousCataloguePolicyRecord CreateDefault(Guid shopId, DateTimeOffset now) => new()
    {
        ShopId = shopId,
        Mode = AutonomousCatalogueMode.Shadow,
        ReviewEveryHours = Math.Clamp(defaults.DefaultReviewEveryHours, 1, 720),
        MaximumCandidatesPerRun = Math.Clamp(defaults.DefaultMaximumCandidatesPerRun, 1, CatalogueAiQueuePreparationService.MaximumBatchSize),
        MaximumAutoPublishesPerDay = Math.Clamp(defaults.DefaultMaximumAutoPublishesPerDay, 1, 100),
        MinimumReadinessScore = Math.Clamp(defaults.DefaultMinimumReadinessScore, .50m, 1m),
        DuplicateHoldConfidence = Math.Clamp(defaults.DefaultDuplicateHoldConfidence, .50m, 1m),
        DailyAiBudgetUsd = Math.Clamp(defaults.DefaultDailyAiBudgetUsd, 0m, 100m),
        CreatedUtc = now,
        UpdatedUtc = now,
        UpdatedBy = "system default"
    };

    private string? Validate(AutonomousCataloguePolicyUpdate update)
    {
        if (string.IsNullOrWhiteSpace(update.ShopSlug)) return "Select a shop before saving the autonomous policy.";
        if (update.Mode == AutonomousCatalogueMode.Automatic && !defaults.AutomaticPublishingEnabled)
            return "Automatic publication is protected by a global safety switch that has not been enabled yet. Use shadow mode while the decisions are calibrated.";
        if (update.ReviewEveryHours is < 1 or > 720) return "Review cadence must be between 1 and 720 hours.";
        if (update.MaximumCandidatesPerRun is < 1 or > CatalogueAiQueuePreparationService.MaximumBatchSize)
            return $"Candidates per run must be between 1 and {CatalogueAiQueuePreparationService.MaximumBatchSize}.";
        if (update.MaximumAutoPublishesPerDay is < 1 or > 100) return "The daily publication limit must be between 1 and 100.";
        if (update.MinimumReadinessScore is < .50m or > 1m) return "The minimum readiness score must be between 0.50 and 1.00.";
        if (update.DuplicateHoldConfidence is < .50m or > 1m) return "The duplicate hold threshold must be between 0.50 and 1.00.";
        if (update.DailyAiBudgetUsd is < 0m or > 100m) return "The daily AI budget must be between USD 0 and USD 100.";
        if (NormaliseActor(update.UpdatedBy).Length > 256) return "The administrator identity is too long to store safely.";
        return null;
    }

    private static AutonomousCataloguePolicy ToPolicy(AutonomousCataloguePolicyRecord row) => new(
        row.ShopId,
        row.Shop.Slug,
        row.Shop.DisplayName,
        row.Mode,
        row.ReviewEveryHours,
        row.MaximumCandidatesPerRun,
        row.MaximumAutoPublishesPerDay,
        row.MinimumReadinessScore,
        row.DuplicateHoldConfidence,
        row.DailyAiBudgetUsd,
        row.UpdatedUtc,
        row.UpdatedBy,
        Convert.ToBase64String(row.RowVersion));

    private static bool MatchesRowVersion(byte[] actual, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected)) return true;
        try { return actual.SequenceEqual(Convert.FromBase64String(expected)); }
        catch (FormatException) { return false; }
    }

    private static string NormaliseActor(string? value) =>
        string.Join(' ', (value ?? "administrator").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
