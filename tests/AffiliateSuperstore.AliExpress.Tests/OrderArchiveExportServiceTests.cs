using System.Text;
using AffiliateSuperstore.Application.Orders;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class OrderArchiveExportServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateCsvAsync_ExportsDurableFieldsAndNeutralisesSpreadsheetFormulae()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await using (var context = factory.CreateDbContext())
        {
            context.AffiliateOrders.Add(new AffiliateOrderRecord
            {
                SubOrderId = "order-1",
                ParentOrderId = "parent,\"one\"",
                Status = AliExpressOrderStatuses.CompletedSettlement,
                ProductId = "product-1",
                ProductTitle = "=HYPERLINK(\"https://example.test\",\"click\")",
                TrackingId = "theplushyshop",
                EstimatedFinishedCommission = 1.25m,
                SettledCurrency = "USD",
                IsAffiliateProduct = true,
                FirstSeenUtc = Now.AddDays(-1),
                LastSeenUtc = Now,
                RawJson = "secret raw API data"
            });
            await context.SaveChangesAsync();
        }

        var export = await new OrderArchiveExportService(factory, new FixedTimeProvider(Now)).CreateCsvAsync();
        var csv = Encoding.UTF8.GetString(export.Content);

        Assert.Equal(1, export.OrderCount);
        Assert.Equal("affiliate-orders-20260830-200000Z.csv", export.FileName);
        Assert.StartsWith("\uFEFFsub_order_id,parent_order_id,status", csv, StringComparison.Ordinal);
        Assert.Contains("\"parent,\"\"one\"\"\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"'=HYPERLINK(\"\"https://example.test\"\",\"\"click\"\")\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("secret raw API data", csv, StringComparison.Ordinal);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }
}
