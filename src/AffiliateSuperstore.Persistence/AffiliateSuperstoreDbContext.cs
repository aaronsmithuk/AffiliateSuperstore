using AffiliateSuperstore.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Persistence;

public sealed class AffiliateSuperstoreDbContext(DbContextOptions<AffiliateSuperstoreDbContext> options)
    : IdentityDbContext<IdentityUser, IdentityRole, string>(options)
{
    public DbSet<ShopRecord> Shops => Set<ShopRecord>();
    public DbSet<ProductRecord> Products => Set<ProductRecord>();
    public DbSet<ShopProductRecord> ShopProducts => Set<ShopProductRecord>();
    public DbSet<CollectionRecord> Collections => Set<CollectionRecord>();
    public DbSet<CollectionProductRecord> CollectionProducts => Set<CollectionProductRecord>();
    public DbSet<EditorialVersionRecord> EditorialVersions => Set<EditorialVersionRecord>();
    public DbSet<ProductSnapshotRecord> ProductSnapshots => Set<ProductSnapshotRecord>();
    public DbSet<ProductMediaRecord> ProductMedia => Set<ProductMediaRecord>();
    public DbSet<ProductChangeEventRecord> ProductChangeEvents => Set<ProductChangeEventRecord>();
    public DbSet<ProductIdentityProfileRecord> ProductIdentityProfiles => Set<ProductIdentityProfileRecord>();
    public DbSet<ProductImageFingerprintRecord> ProductImageFingerprints => Set<ProductImageFingerprintRecord>();
    public DbSet<ProductMatchCandidateRecord> ProductMatchCandidates => Set<ProductMatchCandidateRecord>();
    public DbSet<ProductIdentityGoldLabelRecord> ProductIdentityGoldLabels => Set<ProductIdentityGoldLabelRecord>();
    public DbSet<CanonicalProductRecord> CanonicalProducts => Set<CanonicalProductRecord>();
    public DbSet<CanonicalProductMemberRecord> CanonicalProductMembers => Set<CanonicalProductMemberRecord>();
    public DbSet<AffiliateLinkRecord> AffiliateLinks => Set<AffiliateLinkRecord>();
    public DbSet<OutboundClickRecord> OutboundClicks => Set<OutboundClickRecord>();
    public DbSet<ProductImpressionDailyRecord> ProductImpressions => Set<ProductImpressionDailyRecord>();
    public DbSet<IngestionJobRecord> IngestionJobs => Set<IngestionJobRecord>();
    public DbSet<AutomationWorkItemRecord> AutomationWorkItems => Set<AutomationWorkItemRecord>();
    public DbSet<AutonomousCataloguePolicyRecord> AutonomousCataloguePolicies => Set<AutonomousCataloguePolicyRecord>();
    public DbSet<AutonomousCatalogueDecisionRecord> AutonomousCatalogueDecisions => Set<AutonomousCatalogueDecisionRecord>();
    public DbSet<AiInvocationRecord> AiInvocations => Set<AiInvocationRecord>();
    public DbSet<AffiliateOrderRecord> AffiliateOrders => Set<AffiliateOrderRecord>();
    public DbSet<AffiliateS2sEventRecord> AffiliateS2sEvents => Set<AffiliateS2sEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureShop(modelBuilder);
        ConfigureProduct(modelBuilder);
        ConfigureShopProduct(modelBuilder);
        ConfigureCollection(modelBuilder);
        ConfigureEditorialVersion(modelBuilder);
        ConfigureProductSnapshot(modelBuilder);
        ConfigureProductMedia(modelBuilder);
        ConfigureProductChangeEvent(modelBuilder);
        ConfigureProductIdentity(modelBuilder);
        ConfigureProductImageFingerprint(modelBuilder);
        ConfigureAffiliateLink(modelBuilder);
        ConfigureOutboundClick(modelBuilder);
        ConfigureProductImpression(modelBuilder);
        ConfigureIngestionJob(modelBuilder);
        ConfigureAutomationWorkItem(modelBuilder);
        ConfigureAutonomousCataloguePolicy(modelBuilder);
        ConfigureAutonomousCatalogueDecision(modelBuilder);
        ConfigureAiInvocation(modelBuilder);
        ConfigureAffiliateOrder(modelBuilder);
        ConfigureAffiliateS2sEvent(modelBuilder);
    }

    private static void ConfigureAiInvocation(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AiInvocationRecord>();
        entity.ToTable("AiInvocations");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Purpose).HasMaxLength(50).IsRequired();
        entity.Property(item => item.ProductId).HasMaxLength(64).IsRequired();
        entity.Property(item => item.Provider).HasMaxLength(50).IsRequired();
        entity.Property(item => item.Model).HasMaxLength(100).IsRequired();
        entity.Property(item => item.PromptVersion).HasMaxLength(50).IsRequired();
        entity.Property(item => item.InputHash).HasMaxLength(64).IsRequired();
        entity.Property(item => item.CacheKey).HasMaxLength(64).IsRequired();
        entity.Property(item => item.ProviderResponseId).HasMaxLength(100);
        entity.Property(item => item.ResponseHash).HasMaxLength(64);
        entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.ReservedCostUsd).HasPrecision(18, 8);
        entity.Property(item => item.EstimatedCostUsd).HasPrecision(18, 8);
        entity.Property(item => item.EditorialValidationState).HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.ValidationFindingsJson).HasMaxLength(4000);
        entity.Property(item => item.ErrorCode).HasMaxLength(80);
        entity.Property(item => item.ErrorMessage).HasMaxLength(1000);
        entity.HasIndex(item => new { item.RequestedUtc, item.Status });
        entity.HasIndex(item => new { item.Purpose, item.RequestedUtc });
        entity.HasIndex(item => new { item.CacheKey, item.Status, item.CompletedUtc });
        entity.HasIndex(item => new { item.ProductId, item.RequestedUtc });
    }

    private static void ConfigureShop(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ShopRecord>();
        entity.ToTable("Shops");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Slug).HasMaxLength(80).IsRequired();
        entity.Property(item => item.DisplayName).HasMaxLength(160).IsRequired();
        entity.Property(item => item.PathPrefix).HasMaxLength(200).IsRequired();
        entity.Property(item => item.CanonicalHostname).HasMaxLength(253);
        entity.Property(item => item.TrackingId).HasMaxLength(100).IsRequired();
        entity.Property(item => item.SubAffiliateCode).HasMaxLength(100);
        entity.Property(item => item.DefaultSearchQuery).HasMaxLength(500).IsRequired();
        entity.Property(item => item.SeoTitle).HasMaxLength(200).IsRequired();
        entity.Property(item => item.SeoDescription).HasMaxLength(500).IsRequired();
        entity.Property(item => item.PrimaryColour).HasMaxLength(20).IsRequired();
        entity.Property(item => item.AccentColour).HasMaxLength(20).IsRequired();
        entity.Property(item => item.RowVersion).IsRowVersion();
        entity.HasIndex(item => item.Slug).IsUnique();
        entity.HasIndex(item => new { item.CanonicalHostname, item.PathPrefix }).IsUnique();
        entity.HasIndex(item => item.IsEnabled);
    }

    private static void ConfigureProduct(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProductRecord>();
        entity.ToTable("Products");
        entity.HasKey(item => item.AliExpressProductId);
        entity.Property(item => item.AliExpressProductId).HasMaxLength(64);
        entity.Property(item => item.Title).HasMaxLength(1000).IsRequired();
        entity.Property(item => item.ProductDetailUrl).HasMaxLength(2048);
        entity.Property(item => item.MainImageUrl).HasMaxLength(2048);
        entity.Property(item => item.FirstLevelCategoryId).HasMaxLength(64);
        entity.Property(item => item.FirstLevelCategoryName).HasMaxLength(300);
        entity.Property(item => item.SecondLevelCategoryId).HasMaxLength(64);
        entity.Property(item => item.SecondLevelCategoryName).HasMaxLength(300);
        entity.Property(item => item.SellerId).HasMaxLength(64);
        entity.Property(item => item.SellerName).HasMaxLength(500);
        entity.Property(item => item.SellerUrl).HasMaxLength(2048);
        entity.Property(item => item.SkuId).HasMaxLength(64);
        entity.Property(item => item.EanCode).HasMaxLength(64);
        entity.Property(item => item.IneligibilityReason).HasMaxLength(1000);
        entity.Property(item => item.AvailabilityState).HasConversion<string>().HasMaxLength(30).HasDefaultValue(ProductAvailabilityState.Available);
        entity.Property(item => item.AvailabilityReason).HasMaxLength(1000);
        entity.Property(item => item.CurrentObservationHash).HasMaxLength(64);
        entity.Property(item => item.CurrentContentHash).HasMaxLength(64);
        entity.Property(item => item.RowVersion).IsRowVersion();
        entity.HasIndex(item => item.LastRefreshedUtc);
        entity.HasIndex(item => new { item.IsEligible, item.LastSeenUtc });
        entity.HasIndex(item => item.SellerId);
        entity.HasIndex(item => item.LastDetailRefreshedUtc);
        entity.HasIndex(item => new { item.AvailabilityState, item.LastCheckedUtc });
    }

    private static void ConfigureShopProduct(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ShopProductRecord>();
        entity.ToTable("ShopProducts");
        entity.HasKey(item => new { item.ShopId, item.ProductId });
        entity.Property(item => item.ProductId).HasMaxLength(64);
        entity.Property(item => item.EditorialTitle).HasMaxLength(1000);
        entity.Property(item => item.EditorialDescription).HasMaxLength(4000);
        entity.Property(item => item.VerifiedSize).HasMaxLength(300);
        entity.Property(item => item.VerifiedOptions).HasMaxLength(600);
        entity.Property(item => item.VerificationEvidence).HasMaxLength(1000);
        entity.Property(item => item.EditorialValidationState).HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(EditorialValidationState.NotEvaluated);
        entity.Property(item => item.EditorialValidationFlags).HasMaxLength(4000);
        entity.Property(item => item.DisabledReason).HasMaxLength(1000);
        entity.Property(item => item.AutomatedReviewFlags).HasMaxLength(4000);
        entity.Property(item => item.ReviewStatus).HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.RowVersion).IsRowVersion();
        entity.HasOne(item => item.Shop).WithMany(shop => shop.Products).HasForeignKey(item => item.ShopId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.Product).WithMany(product => product.Shops).HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(item => new { item.ShopId, item.IsActive, item.ReviewStatus, item.IsFeatured, item.DisplayOrder });
    }

    private static void ConfigureEditorialVersion(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<EditorialVersionRecord>();
        entity.ToTable("EditorialVersions");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.ProductId).HasMaxLength(64).IsRequired();
        entity.Property(item => item.EditorialTitle).HasMaxLength(1000);
        entity.Property(item => item.EditorialDescription).HasMaxLength(4000);
        entity.Property(item => item.VerifiedSize).HasMaxLength(300);
        entity.Property(item => item.VerifiedOptions).HasMaxLength(600);
        entity.Property(item => item.VerificationEvidence).HasMaxLength(1000);
        entity.Property(item => item.ChangeKind).HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.ChangeReason).HasMaxLength(500);
        entity.Property(item => item.CreatedBy).HasMaxLength(256).IsRequired();
        entity.Property(item => item.ValidationState).HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.ValidationFindingsJson).HasMaxLength(4000).IsRequired();
        entity.Property(item => item.ValidatorVersion).HasMaxLength(40).IsRequired();
        entity.Property(item => item.ContentHash).HasMaxLength(64).IsRequired();
        entity.HasOne(item => item.ShopProduct).WithMany(item => item.EditorialVersions)
            .HasForeignKey(item => new { item.ShopId, item.ProductId }).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.RolledBackFromVersion).WithMany()
            .HasForeignKey(item => item.RolledBackFromVersionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => new { item.ShopId, item.ProductId, item.VersionNumber }).IsUnique();
        entity.HasIndex(item => new { item.ShopId, item.CreatedUtc });
    }

    private static void ConfigureCollection(ModelBuilder modelBuilder)
    {
        var collection = modelBuilder.Entity<CollectionRecord>();
        collection.ToTable("Collections");
        collection.HasKey(item => item.Id);
        collection.Property(item => item.Slug).HasMaxLength(80).IsRequired();
        collection.Property(item => item.DisplayName).HasMaxLength(160).IsRequired();
        collection.Property(item => item.ShortDescription).HasMaxLength(500).IsRequired();
        collection.Property(item => item.IntroductoryCopy).HasMaxLength(4000).IsRequired();
        collection.Property(item => item.SeoTitle).HasMaxLength(200).IsRequired();
        collection.Property(item => item.SeoDescription).HasMaxLength(500).IsRequired();
        collection.Property(item => item.DiscoveryQueriesJson).HasMaxLength(4000).IsRequired();
        collection.Property(item => item.RowVersion).IsRowVersion();
        collection.HasOne(item => item.Shop).WithMany(shop => shop.Collections)
            .HasForeignKey(item => item.ShopId).OnDelete(DeleteBehavior.Cascade);
        collection.HasIndex(item => new { item.ShopId, item.Slug }).IsUnique();
        collection.HasIndex(item => new { item.ShopId, item.IsPublished, item.DisplayOrder });

        var membership = modelBuilder.Entity<CollectionProductRecord>();
        membership.ToTable("CollectionProducts");
        membership.HasKey(item => new { item.CollectionId, item.ProductId });
        membership.Property(item => item.ProductId).HasMaxLength(64).IsRequired();
        membership.Property(item => item.AssignedBy).HasMaxLength(256).IsRequired();
        membership.HasOne(item => item.Collection).WithMany(item => item.Products)
            .HasForeignKey(item => item.CollectionId).OnDelete(DeleteBehavior.Cascade);
        membership.HasOne(item => item.Product).WithMany(item => item.Collections)
            .HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Cascade);
        membership.HasIndex(item => new { item.CollectionId, item.IsFeatured, item.DisplayOrder });
        membership.HasIndex(item => item.ProductId);
    }

    private static void ConfigureProductSnapshot(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProductSnapshotRecord>();
        entity.ToTable("ProductSnapshots");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.ProductId).HasMaxLength(64);
        entity.Property(item => item.SalePrice).HasPrecision(18, 4);
        entity.Property(item => item.OriginalPrice).HasPrecision(18, 4);
        entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        entity.Property(item => item.CommissionRate).HasPrecision(9, 6);
        entity.Property(item => item.HotProductCommissionRate).HasPrecision(9, 6);
        entity.Property(item => item.DiscountText).HasMaxLength(100);
        entity.Property(item => item.EvaluationRate).HasPrecision(9, 6);
        entity.Property(item => item.TaxRate).HasPrecision(9, 6);
        entity.Property(item => item.ObservationHash).HasMaxLength(64);
        entity.Property(item => item.ContentHash).HasMaxLength(64);
        entity.Property(item => item.SourceEndpoint).HasMaxLength(120);
        entity.Property(item => item.CorrelationId).HasMaxLength(100);
        entity.Property(item => item.ParserVersion).HasMaxLength(40).IsRequired().HasDefaultValue("1.0");
        entity.HasOne(item => item.Product).WithMany(product => product.Snapshots).HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(item => new { item.ProductId, item.FetchedUtc }).IsUnique();
        entity.HasIndex(item => item.FetchedUtc);
        entity.HasIndex(item => new { item.ProductId, item.ContentHash });
    }

    private static void ConfigureProductMedia(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProductMediaRecord>();
        entity.ToTable("ProductMedia");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.ProductId).HasMaxLength(64).IsRequired();
        entity.Property(item => item.Type).HasConversion<string>().HasMaxLength(20);
        entity.Property(item => item.Url).HasMaxLength(2048).IsRequired();
        entity.HasOne(item => item.Product).WithMany(product => product.Media).HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(item => new { item.ProductId, item.Type, item.Position }).IsUnique();
    }

    private static void ConfigureProductChangeEvent(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProductChangeEventRecord>();
        entity.ToTable("ProductChangeEvents");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.ProductId).HasMaxLength(64).IsRequired();
        entity.Property(item => item.Kind).HasConversion<string>().HasMaxLength(40);
        entity.Property(item => item.EvidenceSource).HasMaxLength(120).IsRequired();
        entity.Property(item => item.CorrelationId).HasMaxLength(100);
        entity.Property(item => item.PreviousValue).HasMaxLength(1000);
        entity.Property(item => item.CurrentValue).HasMaxLength(1000);
        entity.Property(item => item.ObservationHash).HasMaxLength(64);
        entity.Property(item => item.DetailsJson).HasMaxLength(4000);
        entity.HasOne(item => item.Product).WithMany(product => product.ChangeEvents).HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(item => new { item.ProductId, item.OccurredUtc });
        entity.HasIndex(item => new { item.Kind, item.OccurredUtc });
    }

    private static void ConfigureProductIdentity(ModelBuilder modelBuilder)
    {
        var profile = modelBuilder.Entity<ProductIdentityProfileRecord>();
        profile.ToTable("ProductIdentityProfiles");
        profile.HasKey(item => item.ProductId);
        profile.Property(item => item.ProductId).HasMaxLength(64);
        profile.Property(item => item.NormalizedTitle).HasMaxLength(1000).IsRequired();
        profile.Property(item => item.NormalizedGtin).HasMaxLength(32);
        profile.Property(item => item.NormalizedModel).HasMaxLength(100);
        profile.Property(item => item.SizeCentimetres).HasPrecision(12, 3);
        profile.Property(item => item.Colour).HasMaxLength(60);
        profile.Property(item => item.Material).HasMaxLength(100);
        profile.Property(item => item.TokensJson).HasMaxLength(4000).IsRequired();
        profile.Property(item => item.InputHash).HasMaxLength(64).IsRequired();
        profile.Property(item => item.NormalizerVersion).HasMaxLength(40).IsRequired();
        profile.HasOne(item => item.Product).WithOne(product => product.IdentityProfile).HasForeignKey<ProductIdentityProfileRecord>(item => item.ProductId).OnDelete(DeleteBehavior.Cascade);
        profile.HasIndex(item => item.NormalizedGtin);
        profile.HasIndex(item => new { item.PackCount, item.SizeCentimetres });

        var candidate = modelBuilder.Entity<ProductMatchCandidateRecord>();
        candidate.ToTable("ProductMatchCandidates");
        candidate.HasKey(item => item.Id);
        candidate.Property(item => item.LeftProductId).HasMaxLength(64).IsRequired();
        candidate.Property(item => item.RightProductId).HasMaxLength(64).IsRequired();
        candidate.Property(item => item.SuggestedRelationship).HasConversion<string>().HasMaxLength(30);
        candidate.Property(item => item.ReviewStatus).HasConversion<string>().HasMaxLength(30);
        candidate.Property(item => item.Confidence).HasPrecision(6, 5);
        candidate.Property(item => item.BlockingReason).HasMaxLength(500).IsRequired();
        candidate.Property(item => item.EvidenceJson).HasMaxLength(4000).IsRequired();
        candidate.Property(item => item.ConflictJson).HasMaxLength(2000);
        candidate.Property(item => item.MatcherVersion).HasMaxLength(40).IsRequired();
        candidate.Property(item => item.ReviewedBy).HasMaxLength(256);
        candidate.Property(item => item.RowVersion).IsRowVersion();
        candidate.HasOne(item => item.LeftProduct).WithMany().HasForeignKey(item => item.LeftProductId).OnDelete(DeleteBehavior.Restrict);
        candidate.HasOne(item => item.RightProduct).WithMany().HasForeignKey(item => item.RightProductId).OnDelete(DeleteBehavior.Restrict);
        candidate.HasIndex(item => new { item.LeftProductId, item.RightProductId, item.MatcherVersion }).IsUnique();
        candidate.HasIndex(item => new { item.ReviewStatus, item.IsCurrent, item.Confidence, item.GeneratedUtc });

        var goldLabel = modelBuilder.Entity<ProductIdentityGoldLabelRecord>();
        goldLabel.ToTable("ProductIdentityGoldLabels");
        goldLabel.HasKey(item => item.Id);
        goldLabel.Property(item => item.Label).HasConversion<string>().HasMaxLength(30);
        goldLabel.Property(item => item.Slice).HasConversion<string>().HasMaxLength(30);
        goldLabel.Property(item => item.Reviewer).HasMaxLength(256).IsRequired();
        goldLabel.Property(item => item.Rationale).HasMaxLength(1000);
        goldLabel.HasOne(item => item.Candidate).WithMany(item => item.GoldLabels)
            .HasForeignKey(item => item.CandidateId).OnDelete(DeleteBehavior.Cascade);
        goldLabel.HasIndex(item => new { item.CandidateId, item.CreatedUtc });
        goldLabel.HasIndex(item => new { item.Slice, item.CreatedUtc });
        goldLabel.HasIndex(item => new { item.Reviewer, item.CreatedUtc });

        var canonical = modelBuilder.Entity<CanonicalProductRecord>();
        canonical.ToTable("CanonicalProducts");
        canonical.HasKey(item => item.Id);
        canonical.Property(item => item.DisplayName).HasMaxLength(500).IsRequired();
        canonical.Property(item => item.RowVersion).IsRowVersion();

        var member = modelBuilder.Entity<CanonicalProductMemberRecord>();
        member.ToTable("CanonicalProductMembers");
        member.HasKey(item => new { item.CanonicalProductId, item.ProductId });
        member.Property(item => item.ProductId).HasMaxLength(64);
        member.Property(item => item.Relationship).HasConversion<string>().HasMaxLength(30);
        member.HasOne(item => item.CanonicalProduct).WithMany(product => product.Members).HasForeignKey(item => item.CanonicalProductId).OnDelete(DeleteBehavior.Cascade);
        member.HasOne(item => item.Product).WithOne(product => product.CanonicalMembership).HasForeignKey<CanonicalProductMemberRecord>(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);
        member.HasOne(item => item.EvidenceCandidate).WithMany().HasForeignKey(item => item.EvidenceCandidateId).OnDelete(DeleteBehavior.SetNull);
        member.HasIndex(item => item.ProductId).IsUnique();
    }

    private static void ConfigureProductImageFingerprint(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProductImageFingerprintRecord>();
        entity.ToTable("ProductImageFingerprints");
        entity.HasKey(item => item.ProductId);
        entity.Property(item => item.ProductId).HasMaxLength(64);
        entity.Property(item => item.SourceUrl).HasMaxLength(2048).IsRequired();
        entity.Property(item => item.SourceUrlHash).HasMaxLength(64).IsRequired();
        entity.Property(item => item.ContentSha256).HasMaxLength(64);
        entity.Property(item => item.ContentType).HasMaxLength(100);
        entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.FailureReason).HasMaxLength(500);
        entity.Property(item => item.FingerprinterVersion).HasMaxLength(40).IsRequired();
        entity.Property(item => item.RowVersion).IsRowVersion();
        entity.HasOne(item => item.Product).WithOne(product => product.ImageFingerprint)
            .HasForeignKey<ProductImageFingerprintRecord>(item => item.ProductId).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(item => item.ContentSha256);
        entity.HasIndex(item => new { item.Status, item.LastAttemptUtc });
    }

    private static void ConfigureAffiliateLink(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AffiliateLinkRecord>();
        entity.ToTable("AffiliateLinks");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.ProductId).HasMaxLength(64);
        entity.Property(item => item.SourceUrl).HasMaxLength(2048).IsRequired();
        entity.Property(item => item.PromotionUrl).HasMaxLength(2048).IsRequired();
        entity.Property(item => item.TrackingId).HasMaxLength(100).IsRequired();
        entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(40);
        entity.Property(item => item.LastError).HasMaxLength(2000);
        entity.Property(item => item.RowVersion).IsRowVersion();
        entity.HasOne(item => item.Shop).WithMany(shop => shop.AffiliateLinks).HasForeignKey(item => item.ShopId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.Product).WithMany(product => product.AffiliateLinks).HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.SetNull);
        entity.HasIndex(item => new { item.ShopId, item.ProductId, item.Status });
        entity.HasIndex(item => item.ExpiresUtc);
    }

    private static void ConfigureOutboundClick(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OutboundClickRecord>();
        entity.ToTable("OutboundClicks");
        entity.HasKey(item => item.ClickId);
        entity.Property(item => item.ClickId).HasMaxLength(64);
        entity.Property(item => item.ProductId).HasMaxLength(64);
        entity.Property(item => item.TrackingId).HasMaxLength(100).IsRequired();
        entity.Property(item => item.Campaign).HasMaxLength(200).IsRequired();
        entity.Property(item => item.Placement).HasMaxLength(100).IsRequired();
        entity.Property(item => item.AnonymousSessionHash).HasMaxLength(128);
        entity.HasOne(item => item.Shop).WithMany(shop => shop.OutboundClicks).HasForeignKey(item => item.ShopId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.Product).WithMany(product => product.OutboundClicks).HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(item => item.AffiliateLink).WithMany(link => link.OutboundClicks).HasForeignKey(item => item.AffiliateLinkId).OnDelete(DeleteBehavior.SetNull);
        entity.HasIndex(item => item.ClickedUtc);
        entity.HasIndex(item => new { item.ShopId, item.Campaign, item.Placement, item.ClickedUtc });
        entity.HasIndex(item => item.ConvertedUtc);
    }

    private static void ConfigureProductImpression(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProductImpressionDailyRecord>();
        entity.ToTable("ProductImpressions");
        entity.HasKey(item => new { item.ShopId, item.ProductId, item.DateUtc, item.Placement });
        entity.Property(item => item.ProductId).HasMaxLength(64);
        entity.Property(item => item.DateUtc).HasColumnType("date");
        entity.Property(item => item.Placement).HasMaxLength(100);
        entity.Property(item => item.RowVersion).IsRowVersion();
        entity.HasOne(item => item.Shop).WithMany(shop => shop.ProductImpressions)
            .HasForeignKey(item => item.ShopId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.Product).WithMany(product => product.Impressions)
            .HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(item => new { item.DateUtc, item.Placement });
        entity.HasIndex(item => new { item.ProductId, item.DateUtc });
    }

    private static void ConfigureIngestionJob(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<IngestionJobRecord>();
        entity.ToTable("IngestionJobs");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Type).HasConversion<string>().HasMaxLength(50);
        entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(50);
        entity.Property(item => item.Checkpoint).HasMaxLength(4000);
        entity.Property(item => item.ErrorSummary).HasMaxLength(4000);
        entity.Property(item => item.CorrelationId).HasMaxLength(100);
        entity.Property(item => item.RowVersion).IsRowVersion();
        entity.HasOne(item => item.Shop).WithMany(shop => shop.IngestionJobs).HasForeignKey(item => item.ShopId).OnDelete(DeleteBehavior.SetNull);
        entity.HasIndex(item => new { item.Status, item.QueuedUtc });
        entity.HasIndex(item => new { item.ShopId, item.Type, item.StartedUtc });
    }

    private static void ConfigureAutomationWorkItem(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AutomationWorkItemRecord>();
        entity.ToTable("AutomationWorkItems");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Type).HasConversion<string>().HasMaxLength(40);
        entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.IdempotencyKey).HasMaxLength(200).IsRequired();
        entity.Property(item => item.PayloadJson).HasMaxLength(4000);
        entity.Property(item => item.Checkpoint).HasMaxLength(1000);
        entity.Property(item => item.LeaseOwner).HasMaxLength(200);
        entity.Property(item => item.LastError).HasMaxLength(2000);
        entity.Property(item => item.CorrelationId).HasMaxLength(100);
        entity.Property(item => item.RowVersion).IsRowVersion();
        entity.HasOne(item => item.Shop).WithMany().HasForeignKey(item => item.ShopId).OnDelete(DeleteBehavior.SetNull);
        entity.HasIndex(item => item.IdempotencyKey).IsUnique();
        entity.HasIndex(item => new { item.Status, item.AvailableUtc, item.Priority });
        entity.HasIndex(item => new { item.ShopId, item.Type, item.QueuedUtc });
        entity.HasIndex(item => item.LeaseExpiresUtc);
    }

    private static void ConfigureAutonomousCataloguePolicy(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AutonomousCataloguePolicyRecord>();
        entity.ToTable("AutonomousCataloguePolicies");
        entity.HasKey(item => item.ShopId);
        entity.Property(item => item.Mode).HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.MinimumReadinessScore).HasPrecision(5, 4);
        entity.Property(item => item.DuplicateHoldConfidence).HasPrecision(5, 4);
        entity.Property(item => item.DailyAiBudgetUsd).HasPrecision(18, 8);
        entity.Property(item => item.UpdatedBy).HasMaxLength(256).IsRequired();
        entity.Property(item => item.RowVersion).IsRowVersion();
        entity.HasOne(item => item.Shop).WithOne()
            .HasForeignKey<AutonomousCataloguePolicyRecord>(item => item.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(item => new { item.Mode, item.UpdatedUtc });
    }

    private static void ConfigureAutonomousCatalogueDecision(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AutonomousCatalogueDecisionRecord>();
        entity.ToTable("AutonomousCatalogueDecisions");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.ProductId).HasMaxLength(64).IsRequired();
        entity.Property(item => item.Mode).HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.Decision).HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.Action).HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.ReadinessScore).HasPrecision(5, 4);
        entity.Property(item => item.ReasonCodesJson).HasMaxLength(2000).IsRequired();
        entity.Property(item => item.Summary).HasMaxLength(1000).IsRequired();
        entity.Property(item => item.EvidenceJson).HasMaxLength(4000).IsRequired();
        entity.Property(item => item.PolicySnapshotJson).HasMaxLength(2000).IsRequired();
        entity.HasOne(item => item.Shop).WithMany().HasForeignKey(item => item.ShopId).OnDelete(DeleteBehavior.NoAction);
        // Keep the audit trail and avoid SQL Server's multiple-cascade-path restriction:
        // Product -> EditorialVersion -> Decision already has a referential action.
        entity.HasOne(item => item.Product).WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.NoAction);
        entity.HasOne(item => item.WorkItem).WithMany().HasForeignKey(item => item.WorkItemId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(item => item.EditorialVersion).WithMany().HasForeignKey(item => item.EditorialVersionId).OnDelete(DeleteBehavior.SetNull);
        entity.HasIndex(item => new { item.ShopId, item.EvaluatedUtc });
        entity.HasIndex(item => new { item.ShopId, item.ProductId, item.EditorialVersionNumber, item.EvaluatedUtc });
        entity.HasIndex(item => new { item.ShopId, item.Action, item.EvaluatedUtc });
    }

    private static void ConfigureAffiliateOrder(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AffiliateOrderRecord>();
        entity.ToTable("AffiliateOrders");
        entity.HasKey(item => item.SubOrderId);
        entity.Property(item => item.SubOrderId).HasMaxLength(100);
        entity.Property(item => item.ParentOrderId).HasMaxLength(100);
        entity.Property(item => item.ClickId).HasMaxLength(64);
        entity.Property(item => item.TrackingId).HasMaxLength(100);
        entity.Property(item => item.CustomParameters).HasMaxLength(1000);
        entity.Property(item => item.Status).HasMaxLength(100).IsRequired();
        entity.Property(item => item.ProductId).HasMaxLength(64);
        entity.Property(item => item.ProductTitle).HasMaxLength(1000);
        entity.Property(item => item.CommissionRate).HasPrecision(9, 6);
        entity.Property(item => item.EstimatedPaidCommission).HasPrecision(18, 4);
        entity.Property(item => item.EstimatedFinishedCommission).HasPrecision(18, 4);
        entity.Property(item => item.IncentiveCommissionRate).HasPrecision(9, 6);
        entity.Property(item => item.EstimatedIncentivePaidCommission).HasPrecision(18, 4);
        entity.Property(item => item.NewBuyerBonusCommission).HasPrecision(18, 4);
        entity.Property(item => item.PaidAmount).HasPrecision(18, 4);
        entity.Property(item => item.FinishedAmount).HasPrecision(18, 4);
        entity.Property(item => item.SettledCurrency).HasMaxLength(3);
        entity.Property(item => item.ShipToCountry).HasMaxLength(2);
        entity.Property(item => item.OrderPlatform).HasMaxLength(50);
        entity.Property(item => item.OrderType).HasMaxLength(50);
        entity.Property(item => item.RowVersion).IsRowVersion();
        entity.HasOne(item => item.Click).WithMany(click => click.Orders).HasForeignKey(item => item.ClickId).OnDelete(DeleteBehavior.SetNull);
        entity.HasIndex(item => item.ClickId);
        entity.HasIndex(item => new { item.Status, item.LastSeenUtc });
        entity.HasIndex(item => item.CompletedSettlementUtc);
    }

    private static void ConfigureAffiliateS2sEvent(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AffiliateS2sEventRecord>();
        entity.ToTable("AffiliateS2sEvents");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.EventKey).HasMaxLength(64).IsRequired();
        entity.Property(item => item.SubOrderId).HasMaxLength(100).IsRequired();
        entity.Property(item => item.ClickId).HasMaxLength(64);
        entity.Property(item => item.ProductId).HasMaxLength(64);
        entity.Property(item => item.TrackingId).HasMaxLength(100);
        entity.Property(item => item.OrderAmount).HasPrecision(18, 4);
        entity.Property(item => item.CommissionRate).HasPrecision(9, 6);
        entity.Property(item => item.EstimatedCommission).HasPrecision(18, 4);
        entity.Property(item => item.IncentiveCommissionRate).HasPrecision(9, 6);
        entity.Property(item => item.IncentiveCommission).HasPrecision(18, 4);
        entity.Property(item => item.NewBuyerBonus).HasPrecision(18, 4);
        entity.Property(item => item.Currency).HasMaxLength(3);
        entity.Property(item => item.ShipToCountry).HasMaxLength(2);
        entity.Property(item => item.OrderPlatform).HasMaxLength(50);
        entity.Property(item => item.OrderType).HasMaxLength(50);
        entity.Property(item => item.PayloadJson).IsRequired();
        entity.HasIndex(item => item.EventKey).IsUnique();
        entity.HasIndex(item => new { item.SubOrderId, item.ReceivedUtc });
        entity.HasIndex(item => item.ClickId);
    }
}
