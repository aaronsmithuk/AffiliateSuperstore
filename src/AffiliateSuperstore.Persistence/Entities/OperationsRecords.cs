namespace AffiliateSuperstore.Persistence.Entities;

public enum AffiliateLinkStatus
{
    Active,
    Expired,
    Invalid,
    GenerationFailed
}

public sealed class AffiliateLinkRecord
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public string? ProductId { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public string PromotionUrl { get; set; } = string.Empty;
    public string TrackingId { get; set; } = string.Empty;
    public int PromotionLinkType { get; set; }
    public AffiliateLinkStatus Status { get; set; } = AffiliateLinkStatus.Active;
    public DateTimeOffset GeneratedUtc { get; set; }
    public DateTimeOffset? LastValidatedUtc { get; set; }
    public DateTimeOffset? ExpiresUtc { get; set; }
    public string? LastError { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ShopRecord Shop { get; set; } = null!;
    public ProductRecord? Product { get; set; }
    public ICollection<OutboundClickRecord> OutboundClicks { get; set; } = [];
}

public sealed class OutboundClickRecord
{
    public string ClickId { get; set; } = string.Empty;
    public Guid ShopId { get; set; }
    public string? ProductId { get; set; }
    public Guid? AffiliateLinkId { get; set; }
    public string TrackingId { get; set; } = string.Empty;
    public string Campaign { get; set; } = string.Empty;
    public string Placement { get; set; } = string.Empty;
    public string? AnonymousSessionHash { get; set; }
    public DateTimeOffset ClickedUtc { get; set; }
    public DateTimeOffset? ConvertedUtc { get; set; }

    public ShopRecord Shop { get; set; } = null!;
    public ProductRecord? Product { get; set; }
    public AffiliateLinkRecord? AffiliateLink { get; set; }
    public ICollection<AffiliateOrderRecord> Orders { get; set; } = [];
}

public sealed class ProductImpressionDailyRecord
{
    public Guid ShopId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public DateOnly DateUtc { get; set; }
    public string Placement { get; set; } = string.Empty;
    public long Count { get; set; }
    public DateTimeOffset FirstSeenUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ShopRecord Shop { get; set; } = null!;
    public ProductRecord Product { get; set; } = null!;
}

public enum IngestionJobType
{
    CatalogueDiscovery,
    ProductRefresh,
    LinkRefresh,
    OrderReconciliation,
    Cleanup
}

public enum IngestionJobStatus
{
    Queued,
    Running,
    Succeeded,
    PartiallySucceeded,
    Failed,
    Cancelled
}

public sealed class IngestionJobRecord
{
    public Guid Id { get; set; }
    public Guid? ShopId { get; set; }
    public IngestionJobType Type { get; set; }
    public IngestionJobStatus Status { get; set; } = IngestionJobStatus.Queued;
    public string? Checkpoint { get; set; }
    public int ItemsRead { get; set; }
    public int ItemsWritten { get; set; }
    public int ItemsRejected { get; set; }
    public int LinksCreatedOrRefreshed { get; set; }
    public DateTimeOffset QueuedUtc { get; set; }
    public DateTimeOffset? StartedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public string? ErrorSummary { get; set; }
    public string? CorrelationId { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ShopRecord? Shop { get; set; }
}

public enum AutomationWorkType
{
    CatalogueDiscovery,
    ProductRefresh,
    IdentityRefresh,
    LinkRefresh,
    AutonomousReview
}

public enum AutomationWorkStatus
{
    Pending,
    Leased,
    Succeeded,
    DeadLetter,
    Cancelled
}

public sealed class AutomationWorkItemRecord
{
    public Guid Id { get; set; }
    public Guid? ShopId { get; set; }
    public AutomationWorkType Type { get; set; }
    public AutomationWorkStatus Status { get; set; } = AutomationWorkStatus.Pending;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
    public string? Checkpoint { get; set; }
    public int Priority { get; set; }
    public DateTimeOffset QueuedUtc { get; set; }
    public DateTimeOffset AvailableUtc { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresUtc { get; set; }
    public int AttemptCount { get; set; }
    public int MaximumAttempts { get; set; } = 5;
    public DateTimeOffset? StartedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public string? LastError { get; set; }
    public string? CorrelationId { get; set; }
    public Guid? ResultJobId { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ShopRecord? Shop { get; set; }
}

public sealed class AffiliateOrderRecord
{
    public string SubOrderId { get; set; } = string.Empty;
    public string? ParentOrderId { get; set; }
    public string? ClickId { get; set; }
    public string? TrackingId { get; set; }
    public string? CustomParameters { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ProductId { get; set; }
    public string? ProductTitle { get; set; }
    public decimal? CommissionRate { get; set; }
    public decimal? EstimatedPaidCommission { get; set; }
    public decimal? EstimatedFinishedCommission { get; set; }
    public decimal? IncentiveCommissionRate { get; set; }
    public decimal? EstimatedIncentivePaidCommission { get; set; }
    public decimal? NewBuyerBonusCommission { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? FinishedAmount { get; set; }
    public string? SettledCurrency { get; set; }
    public DateTimeOffset? PaidUtc { get; set; }
    public DateTimeOffset? FinishedUtc { get; set; }
    public DateTimeOffset? CompletedSettlementUtc { get; set; }
    public string? ShipToCountry { get; set; }
    public bool? IsAffiliateProduct { get; set; }
    public bool? IsHotProduct { get; set; }
    public bool? IsNewBuyer { get; set; }
    public string? OrderPlatform { get; set; }
    public string? OrderType { get; set; }
    public DateTimeOffset FirstSeenUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public string? RawJson { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public OutboundClickRecord? Click { get; set; }
}

public sealed class AffiliateS2sEventRecord
{
    public Guid Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string SubOrderId { get; set; } = string.Empty;
    public string? ClickId { get; set; }
    public string? ProductId { get; set; }
    public string? TrackingId { get; set; }
    public decimal? OrderAmount { get; set; }
    public decimal? CommissionRate { get; set; }
    public decimal? EstimatedCommission { get; set; }
    public decimal? IncentiveCommissionRate { get; set; }
    public decimal? IncentiveCommission { get; set; }
    public decimal? NewBuyerBonus { get; set; }
    public string? Currency { get; set; }
    public string? ShipToCountry { get; set; }
    public bool? IsAffiliateProduct { get; set; }
    public bool? IsHotProduct { get; set; }
    public bool? IsNewBuyer { get; set; }
    public string? OrderPlatform { get; set; }
    public string? OrderType { get; set; }
    public DateTimeOffset? EffectPayUtc { get; set; }
    public DateTimeOffset ReceivedUtc { get; set; }
    public DateTimeOffset ProcessedUtc { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
}
