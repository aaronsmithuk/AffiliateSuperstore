namespace AffiliateSuperstore.Persistence.Entities;

public sealed class ShopRecord
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PathPrefix { get; set; } = string.Empty;
    public string? CanonicalHostname { get; set; }
    public string TrackingId { get; set; } = string.Empty;
    public string? SubAffiliateCode { get; set; }
    public string DefaultSearchQuery { get; set; } = string.Empty;
    public string SeoTitle { get; set; } = string.Empty;
    public string SeoDescription { get; set; } = string.Empty;
    public string PrimaryColour { get; set; } = string.Empty;
    public string AccentColour { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<ShopProductRecord> Products { get; set; } = [];
    public ICollection<AffiliateLinkRecord> AffiliateLinks { get; set; } = [];
    public ICollection<OutboundClickRecord> OutboundClicks { get; set; } = [];
    public ICollection<IngestionJobRecord> IngestionJobs { get; set; } = [];
}

public sealed class ProductRecord
{
    public string AliExpressProductId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ProductDetailUrl { get; set; }
    public string? MainImageUrl { get; set; }
    public string? FirstLevelCategoryId { get; set; }
    public string? FirstLevelCategoryName { get; set; }
    public string? SecondLevelCategoryId { get; set; }
    public string? SecondLevelCategoryName { get; set; }
    public string? SellerId { get; set; }
    public string? SellerName { get; set; }
    public string? SellerUrl { get; set; }
    public string? SkuId { get; set; }
    public string? EanCode { get; set; }
    public bool IsEligible { get; set; } = true;
    public string? IneligibilityReason { get; set; }
    public DateTimeOffset FirstSeenUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public DateTimeOffset LastRefreshedUtc { get; set; }
    public DateTimeOffset? LastDetailRefreshedUtc { get; set; }
    public DateTimeOffset? LastCheckedUtc { get; set; }
    public DateTimeOffset? LastSuccessfulCheckUtc { get; set; }
    public ProductAvailabilityState AvailabilityState { get; set; } = ProductAvailabilityState.Available;
    public string? AvailabilityReason { get; set; }
    public DateTimeOffset? AvailabilityChangedUtc { get; set; }
    public DateTimeOffset? FirstUnavailableEvidenceUtc { get; set; }
    public DateTimeOffset? LastUnavailableEvidenceUtc { get; set; }
    public int ConsecutiveUnavailableChecks { get; set; }
    public string? CurrentObservationHash { get; set; }
    public string? CurrentContentHash { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<ShopProductRecord> Shops { get; set; } = [];
    public ICollection<ProductSnapshotRecord> Snapshots { get; set; } = [];
    public ICollection<ProductMediaRecord> Media { get; set; } = [];
    public ICollection<ProductChangeEventRecord> ChangeEvents { get; set; } = [];
    public ProductIdentityProfileRecord? IdentityProfile { get; set; }
    public ProductImageFingerprintRecord? ImageFingerprint { get; set; }
    public CanonicalProductMemberRecord? CanonicalMembership { get; set; }
    public ICollection<AffiliateLinkRecord> AffiliateLinks { get; set; } = [];
    public ICollection<OutboundClickRecord> OutboundClicks { get; set; } = [];
}

public enum ProductAvailabilityState
{
    Available,
    SuspectedUnavailable,
    Unavailable
}

public enum ProductChangeEventKind
{
    ObservationCreated,
    ContentChanged,
    UnavailableEvidence,
    AvailabilityChanged
}

public sealed class ProductChangeEventRecord
{
    public Guid Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public ProductChangeEventKind Kind { get; set; }
    public DateTimeOffset OccurredUtc { get; set; }
    public string EvidenceSource { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? PreviousValue { get; set; }
    public string? CurrentValue { get; set; }
    public string? ObservationHash { get; set; }
    public string? DetailsJson { get; set; }

    public ProductRecord Product { get; set; } = null!;
}

public sealed class ProductIdentityProfileRecord
{
    public string ProductId { get; set; } = string.Empty;
    public string NormalizedTitle { get; set; } = string.Empty;
    public string? NormalizedGtin { get; set; }
    public string? NormalizedModel { get; set; }
    public int? PackCount { get; set; }
    public decimal? SizeCentimetres { get; set; }
    public string? Colour { get; set; }
    public string? Material { get; set; }
    public string TokensJson { get; set; } = "[]";
    public string InputHash { get; set; } = string.Empty;
    public string NormalizerVersion { get; set; } = "1.0";
    public DateTimeOffset UpdatedUtc { get; set; }

    public ProductRecord Product { get; set; } = null!;
}

public enum ProductImageFingerprintStatus
{
    Succeeded,
    Failed,
    Skipped
}

public sealed class ProductImageFingerprintRecord
{
    public string ProductId { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string SourceUrlHash { get; set; } = string.Empty;
    public string? ContentSha256 { get; set; }
    public long? ContentLength { get; set; }
    public string? ContentType { get; set; }
    public ProductImageFingerprintStatus Status { get; set; }
    public string? FailureReason { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset LastAttemptUtc { get; set; }
    public DateTimeOffset? FingerprintedUtc { get; set; }
    public string FingerprinterVersion { get; set; } = "1.0";
    public byte[] RowVersion { get; set; } = [];

    public ProductRecord Product { get; set; } = null!;
}

public enum ProductRelationship
{
    Primary,
    Duplicate,
    Translation,
    Variant,
    Bundle,
    Related,
    NotRelated
}

public enum ProductMatchReviewStatus
{
    Pending,
    Accepted,
    Rejected
}

public sealed class ProductMatchCandidateRecord
{
    public Guid Id { get; set; }
    public string LeftProductId { get; set; } = string.Empty;
    public string RightProductId { get; set; } = string.Empty;
    public ProductRelationship SuggestedRelationship { get; set; }
    public ProductMatchReviewStatus ReviewStatus { get; set; } = ProductMatchReviewStatus.Pending;
    public bool IsCurrent { get; set; } = true;
    public decimal Confidence { get; set; }
    public string BlockingReason { get; set; } = string.Empty;
    public string EvidenceJson { get; set; } = "{}";
    public string? ConflictJson { get; set; }
    public string MatcherVersion { get; set; } = "1.0";
    public DateTimeOffset GeneratedUtc { get; set; }
    public DateTimeOffset? ReviewedUtc { get; set; }
    public string? ReviewedBy { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ProductRecord LeftProduct { get; set; } = null!;
    public ProductRecord RightProduct { get; set; } = null!;
}

public sealed class CanonicalProductRecord
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<CanonicalProductMemberRecord> Members { get; set; } = [];
}

public sealed class CanonicalProductMemberRecord
{
    public Guid CanonicalProductId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public ProductRelationship Relationship { get; set; }
    public Guid? EvidenceCandidateId { get; set; }
    public DateTimeOffset LinkedUtc { get; set; }

    public CanonicalProductRecord CanonicalProduct { get; set; } = null!;
    public ProductRecord Product { get; set; } = null!;
    public ProductMatchCandidateRecord? EvidenceCandidate { get; set; }
}

public enum ProductMediaType
{
    Image,
    Video
}

public sealed class ProductMediaRecord
{
    public Guid Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public ProductMediaType Type { get; set; }
    public string Url { get; set; } = string.Empty;
    public int Position { get; set; }
    public DateTimeOffset RefreshedUtc { get; set; }

    public ProductRecord Product { get; set; } = null!;
}

public sealed class ShopProductRecord
{
    public Guid ShopId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public ProductReviewStatus ReviewStatus { get; set; } = ProductReviewStatus.Pending;
    public int DisplayOrder { get; set; }
    public string? EditorialTitle { get; set; }
    public string? EditorialDescription { get; set; }
    public int? CurrentEditorialVersionNumber { get; set; }
    public EditorialValidationState EditorialValidationState { get; set; } = EditorialValidationState.NotEvaluated;
    public string? EditorialValidationFlags { get; set; }
    public DateTimeOffset? EditorialValidatedUtc { get; set; }
    public string? DisabledReason { get; set; }
    public string? AutomatedReviewFlags { get; set; }
    public DateTimeOffset? AutomatedReviewedUtc { get; set; }
    public DateTimeOffset FirstIncludedUtc { get; set; }
    public DateTimeOffset LastIncludedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ShopRecord Shop { get; set; } = null!;
    public ProductRecord Product { get; set; } = null!;
    public ICollection<EditorialVersionRecord> EditorialVersions { get; set; } = [];
}

public enum EditorialValidationState
{
    NotEvaluated,
    Passed,
    Warning,
    Blocked
}

public enum EditorialVersionChangeKind
{
    Edit,
    Rollback,
    Imported
}

public sealed class EditorialVersionRecord
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string? EditorialTitle { get; set; }
    public string? EditorialDescription { get; set; }
    public bool IsFeatured { get; set; }
    public int DisplayOrder { get; set; }
    public EditorialVersionChangeKind ChangeKind { get; set; }
    public Guid? RolledBackFromVersionId { get; set; }
    public string? ChangeReason { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
    public EditorialValidationState ValidationState { get; set; }
    public string ValidationFindingsJson { get; set; } = "[]";
    public string ValidatorVersion { get; set; } = "1.0";
    public string ContentHash { get; set; } = string.Empty;

    public ShopProductRecord ShopProduct { get; set; } = null!;
    public EditorialVersionRecord? RolledBackFromVersion { get; set; }
}

public enum ProductReviewStatus
{
    Pending,
    Approved,
    NeedsReview,
    Rejected
}

public sealed class ProductSnapshotRecord
{
    public long Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public DateTimeOffset FetchedUtc { get; set; }
    public decimal? SalePrice { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string Currency { get; set; } = "GBP";
    public decimal? CommissionRate { get; set; }
    public decimal? HotProductCommissionRate { get; set; }
    public string? DiscountText { get; set; }
    public decimal? EvaluationRate { get; set; }
    public long? RecentSalesVolume { get; set; }
    public decimal? TaxRate { get; set; }
    public bool? IsAvailable { get; set; }
    public int? DeliveryDays { get; set; }
    public string? ObservationHash { get; set; }
    public string? ContentHash { get; set; }
    public string? SourceEndpoint { get; set; }
    public string? CorrelationId { get; set; }
    public string ParserVersion { get; set; } = "1.0";
    public string? RawJson { get; set; }

    public ProductRecord Product { get; set; } = null!;
}
