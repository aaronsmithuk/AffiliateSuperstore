namespace AffiliateSuperstore.Persistence.Entities;

public enum AutonomousCatalogueMode
{
    Off,
    Shadow,
    Automatic
}

public enum AutonomousCatalogueDecision
{
    WouldPublish,
    Hold
}

public enum AutonomousCatalogueAction
{
    None,
    ShadowRecorded,
    Published
}

public sealed class AutonomousCataloguePolicyRecord
{
    public Guid ShopId { get; set; }
    public AutonomousCatalogueMode Mode { get; set; } = AutonomousCatalogueMode.Shadow;
    public int ReviewEveryHours { get; set; } = 24;
    public int MaximumCandidatesPerRun { get; set; } = 5;
    public int MaximumAutoPublishesPerDay { get; set; } = 5;
    public decimal MinimumReadinessScore { get; set; } = .98m;
    public decimal DuplicateHoldConfidence { get; set; } = .85m;
    public decimal DailyAiBudgetUsd { get; set; } = .10m;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public string UpdatedBy { get; set; } = "system";
    public byte[] RowVersion { get; set; } = [];

    public ShopRecord Shop { get; set; } = null!;
}

public sealed class AutonomousCatalogueDecisionRecord
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public Guid? WorkItemId { get; set; }
    public Guid? EditorialVersionId { get; set; }
    public int EditorialVersionNumber { get; set; }
    public AutonomousCatalogueMode Mode { get; set; }
    public AutonomousCatalogueDecision Decision { get; set; }
    public AutonomousCatalogueAction Action { get; set; }
    public decimal ReadinessScore { get; set; }
    public string ReasonCodesJson { get; set; } = "[]";
    public string Summary { get; set; } = string.Empty;
    public string EvidenceJson { get; set; } = "{}";
    public string PolicySnapshotJson { get; set; } = "{}";
    public DateTimeOffset EvaluatedUtc { get; set; }

    public ShopRecord Shop { get; set; } = null!;
    public ProductRecord Product { get; set; } = null!;
    public AutomationWorkItemRecord? WorkItem { get; set; }
    public EditorialVersionRecord? EditorialVersion { get; set; }
}
