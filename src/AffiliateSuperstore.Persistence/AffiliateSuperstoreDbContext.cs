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
    public DbSet<ProductSnapshotRecord> ProductSnapshots => Set<ProductSnapshotRecord>();
    public DbSet<ProductMediaRecord> ProductMedia => Set<ProductMediaRecord>();
    public DbSet<ProductChangeEventRecord> ProductChangeEvents => Set<ProductChangeEventRecord>();
    public DbSet<AffiliateLinkRecord> AffiliateLinks => Set<AffiliateLinkRecord>();
    public DbSet<OutboundClickRecord> OutboundClicks => Set<OutboundClickRecord>();
    public DbSet<IngestionJobRecord> IngestionJobs => Set<IngestionJobRecord>();
    public DbSet<AffiliateOrderRecord> AffiliateOrders => Set<AffiliateOrderRecord>();
    public DbSet<AffiliateS2sEventRecord> AffiliateS2sEvents => Set<AffiliateS2sEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureShop(modelBuilder);
        ConfigureProduct(modelBuilder);
        ConfigureShopProduct(modelBuilder);
        ConfigureProductSnapshot(modelBuilder);
        ConfigureProductMedia(modelBuilder);
        ConfigureProductChangeEvent(modelBuilder);
        ConfigureAffiliateLink(modelBuilder);
        ConfigureOutboundClick(modelBuilder);
        ConfigureIngestionJob(modelBuilder);
        ConfigureAffiliateOrder(modelBuilder);
        ConfigureAffiliateS2sEvent(modelBuilder);
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
        entity.Property(item => item.DisabledReason).HasMaxLength(1000);
        entity.Property(item => item.AutomatedReviewFlags).HasMaxLength(4000);
        entity.Property(item => item.ReviewStatus).HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.RowVersion).IsRowVersion();
        entity.HasOne(item => item.Shop).WithMany(shop => shop.Products).HasForeignKey(item => item.ShopId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.Product).WithMany(product => product.Shops).HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(item => new { item.ShopId, item.IsActive, item.ReviewStatus, item.IsFeatured, item.DisplayOrder });
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
