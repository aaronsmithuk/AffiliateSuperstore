namespace AffiliateSuperstore.Application.Catalogue;

public sealed class AutonomousCatalogueOptions
{
    public const string SectionName = "AutonomousCatalogue";

    public bool Enabled { get; set; } = true;
    public bool AutomaticPublishingEnabled { get; set; }
    public int DefaultReviewEveryHours { get; set; } = 24;
    public int DefaultMaximumCandidatesPerRun { get; set; } = 5;
    public int DefaultMaximumAutoPublishesPerDay { get; set; } = 5;
    public decimal DefaultMinimumReadinessScore { get; set; } = .98m;
    public decimal DefaultDuplicateHoldConfidence { get; set; } = .85m;
    public decimal DefaultDailyAiBudgetUsd { get; set; } = .10m;
    public bool AutomaticSafetyCircuitEnabled { get; set; } = true;
    public int AutomaticPauseLookbackHours { get; set; } = 24;
    public int AutomaticPauseConsecutiveFailedAiCalls { get; set; } = 3;
    public bool CollectionSuggestionsEnabled { get; set; }
    public int CollectionSuggestionEveryDays { get; set; } = 7;
    public int MaximumCollectionSuggestionsPerRun { get; set; } = 3;
}
