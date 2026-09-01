namespace AffiliateSuperstore.Persistence.Entities;

public enum AiInvocationStatus
{
    Reserved,
    Succeeded,
    Failed,
    BudgetBlocked,
    CacheHit
}

public sealed class AiInvocationRecord
{
    public Guid Id { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string InputHash { get; set; } = string.Empty;
    public string CacheKey { get; set; } = string.Empty;
    public string? ProviderResponseId { get; set; }
    public string? ResponseHash { get; set; }
    public string? ResponseJson { get; set; }
    public AiInvocationStatus Status { get; set; }
    public DateTimeOffset RequestedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public int AttemptCount { get; set; } = 1;
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public decimal ReservedCostUsd { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public long? LatencyMilliseconds { get; set; }
    public EditorialValidationState EditorialValidationState { get; set; } = EditorialValidationState.NotEvaluated;
    public string? ValidationFindingsJson { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
