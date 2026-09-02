using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Orders;

public enum AffiliateS2sDisposition
{
    Accepted,
    Duplicate,
    Rejected
}

public sealed record AffiliateS2sIngestionResult(
    AffiliateS2sDisposition Disposition,
    string? SubOrderId,
    string? Error);

public sealed class AffiliateS2sIngestionService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    AffiliateS2sOptions options,
    TimeProvider timeProvider)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly HashSet<string> AllowedPayloadFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "order_id", "item_id", "effect_pay_time", "country", "order_amount", "currency",
        "commission_rate", "commission_fee", "clickid", "dp", "tracking_id",
        "is_affiliate_item", "is_hot_product", "platform", "order_type", "category",
        "incentive_commission_rate", "incentive_commission", "is_new_buyer", "new_buyer_bonus"
    };

    public bool IsEnabled => options.Enabled;
    public bool IsConfigured
    {
        get
        {
            try
            {
                AffiliateS2sOptions.Validate(options);
                return options.Enabled;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    public bool IsAuthorized(string? suppliedToken)
    {
        if (!IsConfigured || string.IsNullOrEmpty(suppliedToken)) return false;
        var expected = Encoding.UTF8.GetBytes(options.VerificationToken);
        var supplied = Encoding.UTF8.GetBytes(suppliedToken);
        return expected.Length == supplied.Length && CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    public async Task<AffiliateS2sIngestionResult> IngestAsync(
        IReadOnlyDictionary<string, string> input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        AffiliateS2sOptions.Validate(options);

        var subOrderId = Limit(Get(input, "order_id"), 100);
        if (subOrderId is null)
        {
            return new AffiliateS2sIngestionResult(AffiliateS2sDisposition.Rejected, null, "order_id is required.");
        }

        var payload = input
            .Where(pair => AllowedPayloadFields.Contains(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var payloadJson = JsonSerializer.Serialize(payload);
        if (payloadJson.Length > options.MaximumPayloadCharacters)
        {
            return new AffiliateS2sIngestionResult(AffiliateS2sDisposition.Rejected, subOrderId, "The allowed S2S payload is too large.");
        }

        var clickId = Limit(Get(input, "clickid") ?? Get(input, "dp"), 64);
        var effectPayTime = Get(input, "effect_pay_time");
        var eventKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|',
            subOrderId,
            effectPayTime,
            Get(input, "commission_fee"),
            clickId))));

        await Gate.WaitAsync(cancellationToken);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            if (await context.AffiliateS2sEvents.AnyAsync(item => item.EventKey == eventKey, cancellationToken))
            {
                return new AffiliateS2sIngestionResult(AffiliateS2sDisposition.Duplicate, subOrderId, null);
            }

            var now = timeProvider.GetUtcNow();
            var paidUtc = ParsePacific(effectPayTime);
            OutboundClickRecord? click = null;
            if (clickId is not null)
            {
                click = await context.OutboundClicks.SingleOrDefaultAsync(item => item.ClickId == clickId, cancellationToken);
            }

            var order = await context.AffiliateOrders.SingleOrDefaultAsync(item => item.SubOrderId == subOrderId, cancellationToken);
            if (order is null)
            {
                order = new AffiliateOrderRecord
                {
                    SubOrderId = subOrderId,
                    Status = AliExpressOrderStatuses.PaymentCompleted,
                    FirstSeenUtc = now
                };
                context.AffiliateOrders.Add(order);
            }
            else if (order.Status is not AliExpressOrderStatuses.CompletedSettlement and not AliExpressOrderStatuses.Invalid)
            {
                order.Status = AliExpressOrderStatuses.PaymentCompleted;
            }

            if (click is not null)
            {
                order.ClickId = click.ClickId;
                click.ConvertedUtc ??= paidUtc ?? now;
            }

            order.TrackingId = Limit(Get(input, "tracking_id"), 100) ?? order.TrackingId;
            order.CustomParameters = clickId is null ? order.CustomParameters : JsonSerializer.Serialize(new { dp = clickId });
            order.ProductId = Limit(Get(input, "item_id"), 64) ?? order.ProductId;
            order.CommissionRate = ParseRate(Get(input, "commission_rate")) ?? order.CommissionRate;
            order.EstimatedPaidCommission = ParseDecimal(Get(input, "commission_fee")) ?? order.EstimatedPaidCommission;
            order.IncentiveCommissionRate = ParseRate(Get(input, "incentive_commission_rate")) ?? order.IncentiveCommissionRate;
            order.EstimatedIncentivePaidCommission = ParseDecimal(Get(input, "incentive_commission")) ?? order.EstimatedIncentivePaidCommission;
            order.NewBuyerBonusCommission = ParseDecimal(Get(input, "new_buyer_bonus")) ?? order.NewBuyerBonusCommission;
            order.PaidAmount = ParseDecimal(Get(input, "order_amount")) ?? order.PaidAmount;
            order.SettledCurrency = Limit(Get(input, "currency")?.ToUpperInvariant(), 3) ?? order.SettledCurrency;
            order.PaidUtc = paidUtc ?? order.PaidUtc;
            order.ShipToCountry = Limit(Get(input, "country")?.ToUpperInvariant(), 2) ?? order.ShipToCountry;
            order.IsAffiliateProduct = ParseBoolean(Get(input, "is_affiliate_item")) ?? order.IsAffiliateProduct;
            order.IsHotProduct = ParseBoolean(Get(input, "is_hot_product")) ?? order.IsHotProduct;
            order.IsNewBuyer = ParseBoolean(Get(input, "is_new_buyer")) ?? order.IsNewBuyer;
            order.OrderPlatform = Limit(Get(input, "platform"), 50) ?? order.OrderPlatform;
            order.OrderType = Limit(Get(input, "order_type"), 50) ?? order.OrderType;
            order.LastSeenUtc = now;
            order.RawJson = payloadJson;

            context.AffiliateS2sEvents.Add(new AffiliateS2sEventRecord
            {
                Id = Guid.CreateVersion7(),
                EventKey = eventKey,
                SubOrderId = subOrderId,
                ClickId = clickId,
                ProductId = Limit(Get(input, "item_id"), 64),
                TrackingId = Limit(Get(input, "tracking_id"), 100),
                OrderAmount = ParseDecimal(Get(input, "order_amount")),
                CommissionRate = ParseRate(Get(input, "commission_rate")),
                EstimatedCommission = ParseDecimal(Get(input, "commission_fee")),
                IncentiveCommissionRate = ParseRate(Get(input, "incentive_commission_rate")),
                IncentiveCommission = ParseDecimal(Get(input, "incentive_commission")),
                NewBuyerBonus = ParseDecimal(Get(input, "new_buyer_bonus")),
                Currency = Limit(Get(input, "currency")?.ToUpperInvariant(), 3),
                ShipToCountry = Limit(Get(input, "country")?.ToUpperInvariant(), 2),
                IsAffiliateProduct = ParseBoolean(Get(input, "is_affiliate_item")),
                IsHotProduct = ParseBoolean(Get(input, "is_hot_product")),
                IsNewBuyer = ParseBoolean(Get(input, "is_new_buyer")),
                OrderPlatform = Limit(Get(input, "platform"), 50),
                OrderType = Limit(Get(input, "order_type"), 50),
                EffectPayUtc = paidUtc,
                ReceivedUtc = now,
                ProcessedUtc = now,
                PayloadJson = payloadJson
            });
            await context.SaveChangesAsync(cancellationToken);
            return new AffiliateS2sIngestionResult(AffiliateS2sDisposition.Accepted, subOrderId, null);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string? Get(IReadOnlyDictionary<string, string> input, string name) =>
        input.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static decimal? ParseRate(string? value)
    {
        var parsed = ParseDecimal(value?.TrimEnd('%'));
        if (parsed is null) return null;
        return value?.Contains('%', StringComparison.Ordinal) == true || parsed > 1 ? parsed / 100 : parsed;
    }

    private static bool? ParseBoolean(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "Y" or "YES" or "TRUE" or "1" => true,
        "N" or "NO" or "FALSE" or "0" => false,
        _ => null
    };

    private static DateTimeOffset? ParsePacific(string? value)
    {
        if (!DateTime.TryParseExact(value?.Trim(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) return null;
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var zone = ResolvePacificZone();
        if (zone.IsInvalidTime(local)) local = local.AddHours(1);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }

    private static TimeZoneInfo ResolvePacificZone()
    {
        foreach (var id in new[] { "Pacific Standard Time", "America/Los_Angeles" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }

        throw new InvalidOperationException("A Pacific time-zone definition is required for AliExpress S2S timestamps.");
    }

    private static string? Limit(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maximumLength ? trimmed : trimmed[..maximumLength];
    }
}
