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
    public bool IsEligible { get; set; } = true;
    public string? IneligibilityReason { get; set; }
    public DateTimeOffset FirstSeenUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public DateTimeOffset LastRefreshedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<ShopProductRecord> Shops { get; set; } = [];
    public ICollection<ProductSnapshotRecord> Snapshots { get; set; } = [];
    public ICollection<AffiliateLinkRecord> AffiliateLinks { get; set; } = [];
    public ICollection<OutboundClickRecord> OutboundClicks { get; set; } = [];
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
    public string? DisabledReason { get; set; }
    public string? AutomatedReviewFlags { get; set; }
    public DateTimeOffset? AutomatedReviewedUtc { get; set; }
    public DateTimeOffset FirstIncludedUtc { get; set; }
    public DateTimeOffset LastIncludedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ShopRecord Shop { get; set; } = null!;
    public ProductRecord Product { get; set; } = null!;
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
    public string? RawJson { get; set; }

    public ProductRecord Product { get; set; } = null!;
}
