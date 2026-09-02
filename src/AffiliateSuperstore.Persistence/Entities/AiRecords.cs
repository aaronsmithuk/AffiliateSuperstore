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

public enum CollectionSuggestionStatus
{
    Draft,
    Accepted,
    Rejected
}

public sealed class CollectionSuggestionRecord
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public Guid? AiInvocationId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string IntroductoryCopy { get; set; } = string.Empty;
    public string SeoTitle { get; set; } = string.Empty;
    public string SeoDescription { get; set; } = string.Empty;
    public string DiscoveryQueriesJson { get; set; } = "[]";
    public string Rationale { get; set; } = string.Empty;
    public string EvidenceProductIdsJson { get; set; } = "[]";
    public CollectionSuggestionStatus Status { get; set; } = CollectionSuggestionStatus.Draft;
    public string PromptVersion { get; set; } = string.Empty;
    public string InputHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? ReviewedUtc { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ReviewNote { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ShopRecord Shop { get; set; } = null!;
    public AiInvocationRecord? AiInvocation { get; set; }
}
