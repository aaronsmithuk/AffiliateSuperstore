using System.Globalization;
using System.Text;
using AffiliateSuperstore.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Orders;

public sealed record OrderArchiveExport(string FileName, byte[] Content, int OrderCount);

public sealed class OrderArchiveExportService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    TimeProvider timeProvider)
{
    private const int MaximumRows = 250_000;

    public async Task<OrderArchiveExport> CreateCsvAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var orders = await context.AffiliateOrders.AsNoTracking()
            .OrderBy(order => order.PaidUtc ?? order.FirstSeenUtc)
            .Take(MaximumRows + 1)
            .ToListAsync(cancellationToken);
        if (orders.Count > MaximumRows)
        {
            throw new InvalidOperationException($"The order archive exceeds the safe export limit of {MaximumRows:N0} rows.");
        }

        var csv = new StringBuilder();
        csv.AppendLine("sub_order_id,parent_order_id,status,product_id,product_title,tracking_id,click_id,commission_rate,estimated_paid_commission,estimated_finished_commission,incentive_commission_rate,estimated_incentive_paid_commission,new_buyer_bonus_commission,paid_amount,finished_amount,currency,paid_utc,finished_utc,completed_settlement_utc,ship_to_country,is_affiliate_product,is_hot_product,is_new_buyer,order_platform,order_type,first_seen_utc,last_seen_utc");
        foreach (var order in orders)
        {
            AppendRow(csv,
                order.SubOrderId,
                order.ParentOrderId,
                order.Status,
                order.ProductId,
                order.ProductTitle,
                order.TrackingId,
                order.ClickId,
                Decimal(order.CommissionRate),
                Decimal(order.EstimatedPaidCommission),
                Decimal(order.EstimatedFinishedCommission),
                Decimal(order.IncentiveCommissionRate),
                Decimal(order.EstimatedIncentivePaidCommission),
                Decimal(order.NewBuyerBonusCommission),
                Decimal(order.PaidAmount),
                Decimal(order.FinishedAmount),
                order.SettledCurrency,
                Timestamp(order.PaidUtc),
                Timestamp(order.FinishedUtc),
                Timestamp(order.CompletedSettlementUtc),
                order.ShipToCountry,
                Boolean(order.IsAffiliateProduct),
                Boolean(order.IsHotProduct),
                Boolean(order.IsNewBuyer),
                order.OrderPlatform,
                order.OrderType,
                Timestamp(order.FirstSeenUtc),
                Timestamp(order.LastSeenUtc));
        }

        var body = Encoding.UTF8.GetBytes(csv.ToString());
        var preamble = Encoding.UTF8.GetPreamble();
        var content = new byte[preamble.Length + body.Length];
        preamble.CopyTo(content, 0);
        body.CopyTo(content, preamble.Length);
        var generated = timeProvider.GetUtcNow();
        return new OrderArchiveExport(
            $"affiliate-orders-{generated:yyyyMMdd-HHmmss}Z.csv",
            content,
            orders.Count);
    }

    private static void AppendRow(StringBuilder builder, params string?[] values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            if (index > 0) builder.Append(',');
            builder.Append(Escape(values[index]));
        }

        builder.AppendLine();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var safe = value;
        if (safe[0] is '=' or '+' or '-' or '@' || safe[0] is '	' or '\r') safe = $"'{safe}";
        return safe.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : safe;
    }

    private static string? Decimal(decimal? value) => value?.ToString(CultureInfo.InvariantCulture);
    private static string? Timestamp(DateTimeOffset? value) => value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static string? Boolean(bool? value) => value is null ? null : value.Value ? "Y" : "N";
}
