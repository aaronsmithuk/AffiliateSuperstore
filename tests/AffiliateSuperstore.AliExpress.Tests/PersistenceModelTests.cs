using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class PersistenceModelTests
{
    [Fact]
    public async Task DatabaseModel_PersistsCatalogueClickAndOrderGraph()
    {
        await using var context = CreateContext();
        var now = DateTimeOffset.UtcNow;
        var shopId = Guid.CreateVersion7();
        var linkId = Guid.CreateVersion7();
        var shop = Shop(shopId, now);
        var product = Product(now);

        context.AddRange(
            shop,
            product,
            new ShopProductRecord
            {
                ShopId = shopId,
                ProductId = product.AliExpressProductId,
                FirstIncludedUtc = now,
                LastIncludedUtc = now
            },
            new ProductSnapshotRecord
            {
                ProductId = product.AliExpressProductId,
                FetchedUtc = now,
                SalePrice = 8.99m,
                Currency = "GBP",
                CommissionRate = .07m
            },
            new AffiliateLinkRecord
            {
                Id = linkId,
                ShopId = shopId,
                ProductId = product.AliExpressProductId,
                SourceUrl = "https://www.aliexpress.com/item/1005001.html",
                PromotionUrl = "https://s.click.aliexpress.com/e/test",
                TrackingId = "theplushyshop",
                GeneratedUtc = now
            },
            new OutboundClickRecord
            {
                ClickId = "opaque-click-id",
                ShopId = shopId,
                ProductId = product.AliExpressProductId,
                AffiliateLinkId = linkId,
                TrackingId = "theplushyshop",
                Campaign = "plushies",
                Placement = "product-cta",
                ClickedUtc = now
            },
            new AffiliateOrderRecord
            {
                SubOrderId = "sub-order-1",
                ClickId = "opaque-click-id",
                Status = "Payment Completed",
                ProductId = product.AliExpressProductId,
                FirstSeenUtc = now,
                LastSeenUtc = now
            });

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var stored = await context.AffiliateOrders
            .Include(order => order.Click)
            .ThenInclude(click => click!.AffiliateLink)
            .SingleAsync();

        Assert.Equal("plushies", stored.Click!.Campaign);
        Assert.Equal("https://s.click.aliexpress.com/e/test", stored.Click.AffiliateLink!.PromotionUrl);
        Assert.Equal(.07m, await context.ProductSnapshots.Select(item => item.CommissionRate).SingleAsync());
    }

    [Fact]
    public void DatabaseModel_DefinesCriticalUniqueAndConcurrencyConstraints()
    {
        using var context = CreateContext();
        var shop = context.Model.FindEntityType(typeof(ShopRecord))!;
        var snapshots = context.Model.FindEntityType(typeof(ProductSnapshotRecord))!;
        var product = context.Model.FindEntityType(typeof(ProductRecord))!;
        var changeEvent = context.Model.FindEntityType(typeof(ProductChangeEventRecord))!;
        var workItem = context.Model.FindEntityType(typeof(AutomationWorkItemRecord))!;

        Assert.Contains(shop.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(ShopRecord.Slug));
        Assert.Contains(snapshots.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(ProductSnapshotRecord.ProductId), nameof(ProductSnapshotRecord.FetchedUtc)]));
        Assert.Contains(snapshots.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(ProductSnapshotRecord.ProductId), nameof(ProductSnapshotRecord.ContentHash)]));
        Assert.Contains(product.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(ProductRecord.AvailabilityState), nameof(ProductRecord.LastCheckedUtc)]));
        Assert.Contains(changeEvent.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(ProductChangeEventRecord.ProductId), nameof(ProductChangeEventRecord.OccurredUtc)]));
        Assert.Contains(workItem.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(AutomationWorkItemRecord.IdempotencyKey));
        Assert.True(workItem.FindProperty(nameof(AutomationWorkItemRecord.RowVersion))!.IsConcurrencyToken);
        Assert.True(product.FindProperty(nameof(ProductRecord.RowVersion))!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, product.FindProperty(nameof(ProductRecord.RowVersion))!.ValueGenerated);
    }

    [Fact]
    public void DatabaseModel_IncludesIdentityUsersRolesAndUniqueUserName()
    {
        using var context = CreateContext();
        var user = context.Model.FindEntityType(typeof(IdentityUser));
        var role = context.Model.FindEntityType(typeof(IdentityRole));

        Assert.NotNull(user);
        Assert.NotNull(role);
        Assert.Equal("AspNetUsers", user!.GetTableName());
        Assert.Equal("AspNetRoles", role!.GetTableName());
        Assert.Contains(user.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(IdentityUser.NormalizedUserName)]));
    }

    [Fact]
    public async Task ShopSynchronizer_InsertsUpdatesAndDisablesRemovedConfiguration()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var factory = new InMemoryFactory(databaseName);
        var options = new AffiliateSuperstoreOptions
        {
            Shops =
            [
                new ShopDefinition
                {
                    Slug = "plushies",
                    DisplayName = "The Plushy Shop",
                    PathPrefix = "/plushies/",
                    TrackingId = "theplushyshop",
                    DefaultSearchQuery = "plush toy",
                    SeoTitle = "Plush toys",
                    SeoDescription = "Curated plush toys"
                }
            ]
        };
        var synchronizer = new ShopConfigurationSynchronizer(factory, options, TimeProvider.System);

        await synchronizer.SynchronizeAsync();
        options.Shops[0].DisplayName = "Plushies";
        await synchronizer.SynchronizeAsync();

        await using var context = factory.CreateDbContext();
        var record = await context.Shops.SingleAsync();
        Assert.Equal("Plushies", record.DisplayName);
        Assert.Equal("/plushies", record.PathPrefix);
        Assert.Equal("theplushyshop", record.TrackingId);
    }

    private static AffiliateSuperstoreDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static ShopRecord Shop(Guid id, DateTimeOffset now) => new()
    {
        Id = id,
        Slug = "plushies",
        DisplayName = "The Plushy Shop",
        PathPrefix = "/plushies",
        TrackingId = "theplushyshop",
        DefaultSearchQuery = "plush toy",
        SeoTitle = "Plush toys",
        SeoDescription = "Curated plush toys",
        PrimaryColour = "#000000",
        AccentColour = "#ffffff",
        CreatedUtc = now,
        UpdatedUtc = now
    };

    private static ProductRecord Product(DateTimeOffset now) => new()
    {
        AliExpressProductId = "1005001",
        Title = "Green plush dragon",
        FirstSeenUtc = now,
        LastSeenUtc = now,
        LastRefreshedUtc = now
    };

    private sealed class InMemoryFactory(string databaseName)
        : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }
}
