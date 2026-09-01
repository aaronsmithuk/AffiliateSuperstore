using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AffiliateSuperstore.Application.Catalogue;

public enum AiInvocationStartDisposition
{
    Reserved,
    CacheHit,
    BudgetBlocked,
    AlreadyInProgress
}

public sealed record AiInvocationStart(
    AiInvocationStartDisposition Disposition,
    Guid InvocationId,
    string Message,
    ProductEditorialSuggestionOutput? CachedOutput = null);

public sealed class AiInvocationAuditService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    AiAutomationOptions options,
    TimeProvider timeProvider)
{
    public const string ProductCopyPurpose = "product-copy";

    public async Task<AiInvocationStart> BeginProductCopyAsync(
        ProductEditorialSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var strategyContext = await contextFactory.CreateDbContextAsync(cancellationToken);
        var executionStrategy = strategyContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(
            () => BeginProductCopyCoreAsync(request, cancellationToken));
    }

    private async Task<AiInvocationStart> BeginProductCopyCoreAsync(
        ProductEditorialSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var cacheKey = CacheKey(ProductCopyPurpose, options.Provider, options.Model, request.PromptVersion, request.InputHash);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginSerializableTransactionAsync(context, cancellationToken);

        var cached = await context.AiInvocations
            .AsNoTracking()
            .Where(item => item.CacheKey == cacheKey &&
                           item.Status == AiInvocationStatus.Succeeded &&
                           item.EditorialValidationState != EditorialValidationState.NotEvaluated &&
                           item.ResponseJson != null)
            .OrderByDescending(item => item.CompletedUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (cached is not null)
        {
            var invocationId = Guid.CreateVersion7();
            context.AiInvocations.Add(CreateInvocation(
                invocationId, request, cacheKey, AiInvocationStatus.CacheHit, now, 0m,
                completedUtc: now,
                estimatedCostUsd: 0m,
                providerResponseId: cached.ProviderResponseId,
                responseHash: cached.ResponseHash,
                responseJson: cached.ResponseJson,
                inputTokens: 0,
                outputTokens: 0));
            await context.SaveChangesAsync(cancellationToken);
            await CommitAsync(transaction, cancellationToken);

            var output = JsonSerializer.Deserialize<ProductEditorialSuggestionOutput>(cached.ResponseJson!);
            if (output is null) throw new InvalidOperationException("The cached AI response could not be read.");
            return new AiInvocationStart(
                AiInvocationStartDisposition.CacheHit,
                invocationId,
                "An unchanged, previously validated model response was reused without an API call.",
                output with { InvocationId = invocationId, WasCached = true, InputTokens = 0, OutputTokens = 0 });
        }

        var staleBefore = now.AddMinutes(-Math.Max(1, options.ReservationTimeoutMinutes));
        var staleReservations = await context.AiInvocations
            .Where(item => item.CacheKey == cacheKey &&
                           item.Status == AiInvocationStatus.Reserved &&
                           item.RequestedUtc < staleBefore)
            .ToListAsync(cancellationToken);
        foreach (var stale in staleReservations)
        {
            stale.Status = AiInvocationStatus.Failed;
            stale.CompletedUtc = now;
            stale.EstimatedCostUsd = stale.ReservedCostUsd;
            stale.ErrorCode = "reservation-expired";
            stale.ErrorMessage = "The model call did not complete before its reservation expired; the reserved cost remains charged conservatively.";
        }

        var inProgress = await context.AiInvocations.AnyAsync(
            item => item.CacheKey == cacheKey &&
                    item.Status == AiInvocationStatus.Reserved &&
                    item.RequestedUtc >= staleBefore,
            cancellationToken);
        if (inProgress)
        {
            await context.SaveChangesAsync(cancellationToken);
            await CommitAsync(transaction, cancellationToken);
            return new AiInvocationStart(
                AiInvocationStartDisposition.AlreadyInProgress,
                Guid.Empty,
                "An identical AI request is already in progress.");
        }

        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthEnd = monthStart.AddMonths(1);
        var committedSpend = await context.AiInvocations
            .Where(item => item.RequestedUtc >= monthStart &&
                           item.RequestedUtc < monthEnd &&
                           item.Status != AiInvocationStatus.BudgetBlocked &&
                           item.Status != AiInvocationStatus.CacheHit)
            .SumAsync(
                item => item.Status == AiInvocationStatus.Reserved
                    ? item.ReservedCostUsd
                    : item.EstimatedCostUsd,
                cancellationToken);
        var reservation = options.MaximumReservedCostPerCallUsd;
        var invocationIdForCall = Guid.CreateVersion7();
        if (committedSpend + reservation > options.MonthlyBudgetUsd)
        {
            context.AiInvocations.Add(CreateInvocation(
                invocationIdForCall, request, cacheKey, AiInvocationStatus.BudgetBlocked, now, 0m,
                completedUtc: now,
                estimatedCostUsd: 0m,
                errorCode: "monthly-budget-exhausted",
                errorMessage: $"The configured monthly AI budget of USD {options.MonthlyBudgetUsd:F2} would be exceeded."));
            await context.SaveChangesAsync(cancellationToken);
            await CommitAsync(transaction, cancellationToken);
            return new AiInvocationStart(
                AiInvocationStartDisposition.BudgetBlocked,
                invocationIdForCall,
                $"The monthly AI budget of USD {options.MonthlyBudgetUsd:F2} has been reached.");
        }

        context.AiInvocations.Add(CreateInvocation(
            invocationIdForCall, request, cacheKey, AiInvocationStatus.Reserved, now, reservation));
        await context.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        return new AiInvocationStart(
            AiInvocationStartDisposition.Reserved,
            invocationIdForCall,
            $"USD {reservation:F4} reserved against the monthly AI budget.");
    }

    public async Task RecordSuccessAsync(
        Guid invocationId,
        ProductEditorialSuggestionOutput output,
        string? providerResponseId,
        long latencyMilliseconds,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var invocation = await context.AiInvocations.SingleAsync(item => item.Id == invocationId, cancellationToken);
        if (invocation.Status != AiInvocationStatus.Reserved) throw new InvalidOperationException("The AI invocation is not reserved.");

        var cacheOutput = output with { InvocationId = null, WasCached = false };
        invocation.Status = AiInvocationStatus.Succeeded;
        invocation.CompletedUtc = timeProvider.GetUtcNow();
        invocation.ProviderResponseId = Trim(providerResponseId, 100);
        invocation.ResponseHash = output.ResponseHash;
        invocation.ResponseJson = JsonSerializer.Serialize(cacheOutput);
        invocation.InputTokens = output.InputTokens;
        invocation.OutputTokens = output.OutputTokens;
        invocation.EstimatedCostUsd = output.InputTokens.HasValue && output.OutputTokens.HasValue
            ? options.EstimateCostUsd(output.InputTokens.Value, output.OutputTokens.Value)
            : invocation.ReservedCostUsd;
        invocation.LatencyMilliseconds = Math.Max(0, latencyMilliseconds);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordFailureAsync(
        Guid invocationId,
        string errorCode,
        string errorMessage,
        long latencyMilliseconds,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var invocation = await context.AiInvocations.SingleOrDefaultAsync(item => item.Id == invocationId, cancellationToken);
        if (invocation is null || invocation.Status != AiInvocationStatus.Reserved) return;
        invocation.Status = AiInvocationStatus.Failed;
        invocation.CompletedUtc = timeProvider.GetUtcNow();
        invocation.EstimatedCostUsd = invocation.ReservedCostUsd;
        invocation.LatencyMilliseconds = Math.Max(0, latencyMilliseconds);
        invocation.ErrorCode = Trim(errorCode, 80);
        invocation.ErrorMessage = Trim(errorMessage, 1000);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordValidationAsync(
        Guid? invocationId,
        EditorialValidationResult validation,
        CancellationToken cancellationToken = default)
    {
        if (invocationId is null) return;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var invocation = await context.AiInvocations.SingleOrDefaultAsync(item => item.Id == invocationId, cancellationToken);
        if (invocation is null) return;
        invocation.EditorialValidationState = validation.State;
        invocation.ValidationFindingsJson = Truncate(validation.SerializedFindings, 4000);
        await context.SaveChangesAsync(cancellationToken);
    }

    private AiInvocationRecord CreateInvocation(
        Guid id,
        ProductEditorialSuggestionRequest request,
        string cacheKey,
        AiInvocationStatus status,
        DateTimeOffset requestedUtc,
        decimal reservedCostUsd,
        DateTimeOffset? completedUtc = null,
        decimal estimatedCostUsd = 0m,
        string? providerResponseId = null,
        string? responseHash = null,
        string? responseJson = null,
        int? inputTokens = null,
        int? outputTokens = null,
        string? errorCode = null,
        string? errorMessage = null) => new()
        {
            Id = id,
            Purpose = ProductCopyPurpose,
            ProductId = request.ProductId,
            Provider = options.Provider.Trim(),
            Model = options.Model.Trim(),
            PromptVersion = request.PromptVersion,
            InputHash = request.InputHash,
            CacheKey = cacheKey,
            Status = status,
            RequestedUtc = requestedUtc,
            CompletedUtc = completedUtc,
            ReservedCostUsd = reservedCostUsd,
            EstimatedCostUsd = estimatedCostUsd,
            ProviderResponseId = providerResponseId,
            ResponseHash = responseHash,
            ResponseJson = responseJson,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };

    private static async Task<IDbContextTransaction?> BeginSerializableTransactionAsync(
        AffiliateSuperstoreDbContext context,
        CancellationToken cancellationToken) =>
        context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

    private static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction is null ? Task.CompletedTask : transaction.CommitAsync(cancellationToken);

    private static string CacheKey(string purpose, string provider, string model, string promptVersion, string inputHash)
    {
        var value = $"{purpose}\n{provider.Trim().ToLowerInvariant()}\n{model.Trim()}\n{promptVersion}\n{inputHash}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static string? Trim(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length <= maximumLength ? value : value[..maximumLength];
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];
}
