using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Reporting;

public sealed record AffiliateCommissionSummary(
    string Currency,
    decimal EstimatedCommission,
    decimal SettledCommission);

public sealed record AffiliatePerformanceBreakdown(
    string Name,
    string Detail,
    long Impressions,
    int Clicks,
    int ConvertingClicks,
    int Orders,
    IReadOnlyList<AffiliateCommissionSummary> Commission)
{
    public decimal ClickThroughRate => Impressions == 0 ? 0 : (decimal)Clicks / Impressions;
}

public sealed record AffiliatePerformanceReport(
    int LookbackDays,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset GeneratedUtc,
    int ActiveLinks,
    int ActiveLinksClicked,
    long Impressions,
    int Clicks,
    int ConvertingClicks,
    int AttributedOrders,
    int InvalidOrders,
    int S2sEvents,
    IReadOnlyList<AffiliateCommissionSummary> Commission,
    IReadOnlyList<AffiliatePerformanceBreakdown> Channels,
    IReadOnlyList<AffiliatePerformanceBreakdown> Products)
{
    public decimal ClickThroughRate => Impressions == 0 ? 0 : (decimal)Clicks / Impressions;
    public decimal ClickToOrderRate => Clicks == 0 ? 0 : (decimal)ConvertingClicks / Clicks;
    public int ActiveLinksWithoutClicks => Math.Max(0, ActiveLinks - ActiveLinksClicked);
}

public sealed class AffiliatePerformanceService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    TimeProvider timeProvider)
{
    public async Task<AffiliatePerformanceReport> GetAsync(
        int lookbackDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (lookbackDays is < 1 or > 365) throw new ArgumentOutOfRangeException(nameof(lookbackDays));
        var now = timeProvider.GetUtcNow();
        var windowStart = now.AddDays(-lookbackDays);
        var firstDate = DateOnly.FromDateTime(windowStart.UtcDateTime);
        var lastDate = DateOnly.FromDateTime(now.UtcDateTime);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var activeLinkIds = await context.AffiliateLinks.AsNoTracking()
            .Where(link => link.Status == AffiliateLinkStatus.Active)
            .Select(link => link.Id)
            .ToArrayAsync(cancellationToken);
        var clicks = await context.OutboundClicks.AsNoTracking()
            .Where(click => click.ClickedUtc >= windowStart && click.ClickedUtc <= now)
            .Select(click => new ClickRow(
                click.ClickId,
                click.AffiliateLinkId,
                click.ProductId,
                click.Product == null ? null : click.Product.Title,
                click.Campaign,
                click.Placement))
            .ToListAsync(cancellationToken);
        var impressions = await context.ProductImpressions.AsNoTracking()
            .Where(item => item.DateUtc >= firstDate && item.DateUtc <= lastDate)
            .Select(item => new ImpressionRow(
                item.Shop.Slug,
                item.ProductId,
                item.Product.Title,
                item.Placement,
                item.Count))
            .ToListAsync(cancellationToken);
        var clickIds = clicks.Select(click => click.ClickId).ToArray();
        var orders = clickIds.Length == 0
            ? []
            : await context.AffiliateOrders.AsNoTracking()
                .Where(order => order.ClickId != null && clickIds.Contains(order.ClickId))
                .Select(order => new OrderRow(
                    order.ClickId!,
                    order.Status,
                    order.SettledCurrency,
                    order.EstimatedPaidCommission,
                    order.EstimatedFinishedCommission,
                    order.EstimatedIncentivePaidCommission,
                    order.NewBuyerBonusCommission))
                .ToListAsync(cancellationToken);
        var s2sEvents = await context.AffiliateS2sEvents.AsNoTracking()
            .CountAsync(item => item.ReceivedUtc >= windowStart && item.ReceivedUtc <= now, cancellationToken);

        var orderLookup = orders.ToLookup(order => order.ClickId, StringComparer.Ordinal);
        var convertingClicks = clicks.Count(click => orderLookup.Contains(click.ClickId));
        var clickedActiveLinks = clicks
            .Where(click => click.AffiliateLinkId is not null && activeLinkIds.Contains(click.AffiliateLinkId.Value))
            .Select(click => click.AffiliateLinkId!.Value)
            .Distinct()
            .Count();

        return new AffiliatePerformanceReport(
            lookbackDays,
            windowStart,
            now,
            activeLinkIds.Length,
            clickedActiveLinks,
            impressions.Sum(item => item.Count),
            clicks.Count,
            convertingClicks,
            orders.Count,
            orders.Count(order => order.Status == AliExpressOrderStatuses.Invalid),
            s2sEvents,
            SummariseCommission(orders),
            BuildBreakdown(
                clicks,
                impressions,
                orderLookup,
                click => (click.Campaign, click.Placement),
                impression => (impression.ShopSlug, impression.Placement),
                key => string.IsNullOrWhiteSpace(key.Item1) ? "Unassigned campaign" : key.Item1,
                key => string.IsNullOrWhiteSpace(key.Item2) ? "Unknown placement" : key.Item2),
            BuildBreakdown(
                clicks,
                impressions,
                orderLookup,
                click => (click.ProductId ?? "untracked", click.ProductTitle ?? "Product unavailable"),
                impression => (impression.ProductId, impression.ProductTitle),
                key => key.Item2,
                key => key.Item1));
    }

    private static IReadOnlyList<AffiliatePerformanceBreakdown> BuildBreakdown<TKey>(
        IReadOnlyList<ClickRow> clicks,
        IReadOnlyList<ImpressionRow> impressions,
        ILookup<string, OrderRow> orderLookup,
        Func<ClickRow, TKey> clickKeySelector,
        Func<ImpressionRow, TKey> impressionKeySelector,
        Func<TKey, string> nameSelector,
        Func<TKey, string> detailSelector)
        where TKey : notnull => clicks.Select(clickKeySelector)
            .Concat(impressions.Select(impressionKeySelector))
            .Distinct()
            .Select(key =>
            {
                var groupClicks = clicks.Where(click => EqualityComparer<TKey>.Default.Equals(clickKeySelector(click), key)).ToArray();
                var groupImpressions = impressions
                    .Where(impression => EqualityComparer<TKey>.Default.Equals(impressionKeySelector(impression), key))
                    .Sum(impression => impression.Count);
                var groupOrders = groupClicks.SelectMany(click => orderLookup[click.ClickId]).ToArray();
                return new AffiliatePerformanceBreakdown(
                    nameSelector(key),
                    detailSelector(key),
                    groupImpressions,
                    groupClicks.Length,
                    groupClicks.Count(click => orderLookup.Contains(click.ClickId)),
                    groupOrders.Length,
                    SummariseCommission(groupOrders));
            })
            .OrderByDescending(row => row.Impressions)
            .ThenByDescending(row => row.Clicks)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<AffiliateCommissionSummary> SummariseCommission(IEnumerable<OrderRow> source) => source
        .Where(order =>
            order.Status != AliExpressOrderStatuses.Invalid &&
            !string.IsNullOrWhiteSpace(order.Currency))
        .GroupBy(order => order.Currency!, StringComparer.OrdinalIgnoreCase)
        .Select(group => new AffiliateCommissionSummary(
            group.Key.ToUpperInvariant(),
            group.Sum(EstimatedCommission),
            group.Where(order => order.Status == AliExpressOrderStatuses.CompletedSettlement).Sum(SettledCommission)))
        .OrderBy(summary => summary.Currency, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static decimal EstimatedCommission(OrderRow order) =>
        (order.EstimatedPaidCommission ?? 0) +
        (order.EstimatedIncentivePaidCommission ?? 0) +
        (order.NewBuyerBonusCommission ?? 0);

    private static decimal SettledCommission(OrderRow order) =>
        (order.EstimatedFinishedCommission ?? 0) +
        (order.EstimatedIncentivePaidCommission ?? 0) +
        (order.NewBuyerBonusCommission ?? 0);

    private sealed record ClickRow(
        string ClickId,
        Guid? AffiliateLinkId,
        string? ProductId,
        string? ProductTitle,
        string Campaign,
        string Placement);

    private sealed record ImpressionRow(
        string ShopSlug,
        string ProductId,
        string ProductTitle,
        string Placement,
        long Count);

    private sealed record OrderRow(
        string ClickId,
        string Status,
        string? Currency,
        decimal? EstimatedPaidCommission,
        decimal? EstimatedFinishedCommission,
        decimal? EstimatedIncentivePaidCommission,
        decimal? NewBuyerBonusCommission);
}
