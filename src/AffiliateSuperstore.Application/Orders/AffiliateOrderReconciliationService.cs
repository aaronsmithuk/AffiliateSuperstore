using System.Globalization;
using System.Text.Json;
using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Orders;

public sealed record OrderReconciliationResult(
    Guid? JobId,
    IngestionJobStatus Status,
    int OrdersRead,
    int OrdersWritten,
    int OrdersRejected,
    int AttributedOrders,
    bool WasFullBackfill,
    string? Error);

public enum OrderReconciliationRunMode
{
    Automatic,
    Incremental,
    FullBackfill
}

public sealed class AffiliateOrderReconciliationService(
    IAliExpressClient aliExpressClient,
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    OrderReconciliationOptions options,
    TimeProvider timeProvider)
{
    private const string CommonRequestedFields =
        "sub_order_id,order_id,order_status,product_id,product_title,tracking_id," +
        "commission_rate,estimated_paid_commission,estimated_finished_commission,paid_amount,finished_amount," +
        "settled_currency,paid_time,finished_time,completed_settlement_time,ship_to_country," +
        "is_affiliate_product,is_hot_product,incentive_commission_rate,estimated_incentive_paid_commission," +
        "new_buyer_bonus_commission,is_new_buyer,order_platform,order_type";
    private const string ListRequestedFields = CommonRequestedFields + ",custom_parameters";
    private const string GetRequestedFields = CommonRequestedFields + ",customer_parameters";
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<OrderReconciliationResult> RunAsync(
        OrderReconciliationRunMode runMode = OrderReconciliationRunMode.Automatic,
        CancellationToken cancellationToken = default)
    {
        OrderReconciliationPlanner.Validate(options);
        if (!await Gate.WaitAsync(0, cancellationToken))
        {
            return new OrderReconciliationResult(
                null,
                IngestionJobStatus.Running,
                0,
                0,
                0,
                0,
                false,
                "Order reconciliation is already running in this application instance.");
        }

        Guid? jobId = null;
        try
        {
            var now = timeProvider.GetUtcNow();
            DateTimeOffset? latestSuccessUtc;
            DateTimeOffset? latestFullBackfillUtc;
            bool fullBackfill;
            await using (var setup = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                latestSuccessUtc = await setup.IngestionJobs.AsNoTracking()
                    .Where(job =>
                        job.Type == IngestionJobType.OrderReconciliation &&
                        (job.Status == IngestionJobStatus.Succeeded || job.Status == IngestionJobStatus.PartiallySucceeded) &&
                        job.CompletedUtc != null)
                    .OrderByDescending(job => job.CompletedUtc)
                    .Select(job => job.CompletedUtc)
                    .FirstOrDefaultAsync(cancellationToken);
                latestFullBackfillUtc = await setup.IngestionJobs.AsNoTracking()
                    .Where(job =>
                        job.Type == IngestionJobType.OrderReconciliation &&
                        (job.Status == IngestionJobStatus.Succeeded || job.Status == IngestionJobStatus.PartiallySucceeded) &&
                        job.CompletedUtc != null &&
                        job.Checkpoint != null &&
                        job.Checkpoint.Contains("\"isFullBackfill\":true"))
                    .OrderByDescending(job => job.CompletedUtc)
                    .Select(job => job.CompletedUtc)
                    .FirstOrDefaultAsync(cancellationToken);

                fullBackfill = runMode == OrderReconciliationRunMode.FullBackfill ||
                    runMode == OrderReconciliationRunMode.Automatic &&
                    (latestFullBackfillUtc is null || latestFullBackfillUtc <= now.AddDays(-options.FullBackfillEveryDays));

                jobId = Guid.CreateVersion7();
                setup.IngestionJobs.Add(new IngestionJobRecord
                {
                    Id = jobId.Value,
                    Type = IngestionJobType.OrderReconciliation,
                    Status = IngestionJobStatus.Running,
                    QueuedUtc = now,
                    StartedUtc = now,
                    CorrelationId = jobId.Value.ToString("N"),
                    Checkpoint = Checkpoint("starting", null, null, null, null, fullBackfill)
                });
                await setup.SaveChangesAsync(cancellationToken);
            }

            var windowStartUtc = fullBackfill
                ? now.AddDays(-options.InitialLookbackDays)
                : latestSuccessUtc?.AddHours(-options.IncrementalLookbackHours) ?? now.AddDays(-options.InitialLookbackDays);
            var startPacific = PacificClock.FromUtc(windowStartUtc);
            var endPacific = PacificClock.FromUtc(now);
            foreach (var status in AliExpressOrderStatuses.All)
            {
                string? cursor = null;
                for (var pageNumber = 1; pageNumber <= options.MaximumPagesPerStatus; pageNumber++)
                {
                    var response = await aliExpressClient.ListOrdersByIndexAsync(new AliExpressOrderListByIndexRequest
                    {
                        StartTimePacific = startPacific,
                        EndTimePacific = endPacific,
                        Status = status,
                        TimeType = AliExpressOrderTimeTypes.PaymentCompleted,
                        Fields = ListRequestedFields,
                        PageSize = options.PageSize,
                        StartQueryIndexId = cursor
                    }, cancellationToken);
                    var page = ReadOrders(response);
                    await PersistPageAsync(
                        jobId.Value,
                        page.Items,
                        Checkpoint("discovery", status, pageNumber, page.MaximumQueryIndexId, windowStartUtc, fullBackfill),
                        cancellationToken);

                    var nextCursor = page.MaximumQueryIndexId;
                    if (page.Items.Count < options.PageSize ||
                        string.IsNullOrWhiteSpace(nextCursor) ||
                        string.Equals(cursor, nextCursor, StringComparison.Ordinal))
                    {
                        break;
                    }

                    cursor = nextCursor;
                    if (pageNumber == options.MaximumPagesPerStatus)
                    {
                        throw new InvalidOperationException($"Order query for status '{status}' exceeded the configured page safety limit.");
                    }
                }
            }

            await RefreshOpenOrdersAsync(jobId.Value, fullBackfill, cancellationToken);
            await CompleteAsync(jobId.Value, windowStartUtc, fullBackfill, cancellationToken);
            return await BuildResultAsync(jobId.Value, null, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (jobId is not null) await MarkStoppedAsync(jobId.Value, IngestionJobStatus.Cancelled, "The reconciliation run was cancelled.");
            throw;
        }
        catch (Exception exception)
        {
            if (jobId is null)
            {
                return new OrderReconciliationResult(null, IngestionJobStatus.Failed, 0, 0, 0, 0, false, exception.Message);
            }

            await MarkStoppedAsync(jobId.Value, IngestionJobStatus.Failed, $"{exception.GetType().Name}: {exception.Message}");
            return await BuildResultAsync(jobId.Value, exception.Message, CancellationToken.None);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task RefreshOpenOrdersAsync(Guid jobId, bool fullBackfill, CancellationToken cancellationToken)
    {
        string[] orderIds;
        await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            orderIds = await context.AffiliateOrders.AsNoTracking()
                .Where(order =>
                    order.Status != AliExpressOrderStatuses.CompletedSettlement &&
                    order.Status != AliExpressOrderStatuses.Invalid)
                .OrderBy(order => order.PaidUtc)
                .Select(order => order.SubOrderId)
                .ToArrayAsync(cancellationToken);
        }

        var batches = orderIds.Chunk(options.OpenOrderBatchSize).ToArray();
        for (var batchNumber = 0; batchNumber < batches.Length; batchNumber++)
        {
            var response = await aliExpressClient.GetOrdersAsync(new AliExpressOrderGetRequest
            {
                OrderIds = batches[batchNumber],
                Fields = GetRequestedFields
            }, cancellationToken);
            var page = ReadOrders(response);
            await PersistPageAsync(
                jobId,
                page.Items,
                Checkpoint("open-order-refresh", null, batchNumber + 1, null, null, fullBackfill),
                cancellationToken);
        }

    }

    private async Task PersistPageAsync(
        Guid jobId,
        IReadOnlyList<AliExpressOrder> sourceOrders,
        string checkpoint,
        CancellationToken cancellationToken)
    {
        var rejected = sourceOrders.Count(order => !IsValidOrderId(order.SubOrderId));
        var orders = sourceOrders
            .Where(order => IsValidOrderId(order.SubOrderId))
            .GroupBy(order => order.SubOrderId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();
        var ids = orders.Select(order => order.SubOrderId).ToArray();
        var clickCandidates = orders
            .Select(order => ExtractClickId(order.CustomParameters))
            .Where(clickId => clickId is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.AffiliateOrders
            .Where(order => ids.Contains(order.SubOrderId))
            .ToDictionaryAsync(order => order.SubOrderId, StringComparer.Ordinal, cancellationToken);
        var clicks = await context.OutboundClicks
            .Where(click => clickCandidates.Contains(click.ClickId))
            .ToDictionaryAsync(click => click.ClickId, StringComparer.Ordinal, cancellationToken);
        var now = timeProvider.GetUtcNow();

        foreach (var source in orders)
        {
            if (!existing.TryGetValue(source.SubOrderId, out var target))
            {
                target = new AffiliateOrderRecord
                {
                    SubOrderId = source.SubOrderId,
                    FirstSeenUtc = now
                };
                context.AffiliateOrders.Add(target);
                existing.Add(target.SubOrderId, target);
            }

            var clickId = ExtractClickId(source.CustomParameters);
            if (clickId is not null && clicks.TryGetValue(clickId, out var click))
            {
                target.ClickId = clickId;
                click.ConvertedUtc ??= PacificClock.Parse(source.PaidTime) ?? now;
            }

            target.ParentOrderId = Limit(source.ParentOrderId, 100);
            target.TrackingId = Limit(source.TrackingId, 100);
            target.CustomParameters = Limit(source.CustomParameters, 1000);
            target.Status = Limit(source.Status, 100) ?? "Unknown";
            target.ProductId = Limit(source.ProductId, 64);
            target.ProductTitle = Limit(source.ProductTitle, 1000);
            target.CommissionRate = ParseRate(source.CommissionRate);
            target.EstimatedPaidCommission = source.EstimatedPaidCommission;
            target.EstimatedFinishedCommission = source.EstimatedFinishedCommission;
            target.IncentiveCommissionRate = ParseRate(source.IncentiveCommissionRate);
            target.EstimatedIncentivePaidCommission = source.EstimatedIncentivePaidCommission;
            target.NewBuyerBonusCommission = source.NewBuyerBonusCommission;
            target.PaidAmount = source.PaidAmount;
            target.FinishedAmount = source.FinishedAmount;
            target.SettledCurrency = Limit(source.SettledCurrency?.ToUpperInvariant(), 3);
            target.PaidUtc = PacificClock.Parse(source.PaidTime);
            target.FinishedUtc = PacificClock.Parse(source.FinishedTime);
            target.CompletedSettlementUtc = PacificClock.Parse(source.CompletedSettlementTime);
            target.ShipToCountry = Limit(source.ShipToCountry?.ToUpperInvariant(), 2);
            target.IsAffiliateProduct = source.IsAffiliateProduct;
            target.IsHotProduct = source.IsHotProduct;
            target.IsNewBuyer = source.IsNewBuyer;
            target.OrderPlatform = Limit(source.OrderPlatform, 50);
            target.OrderType = Limit(source.OrderType, 50);
            target.LastSeenUtc = now;
            target.RawJson = source.RawJson ?? JsonSerializer.Serialize(source);
        }

        var job = await context.IngestionJobs.SingleAsync(item => item.Id == jobId, cancellationToken);
        job.ItemsRead += sourceOrders.Count;
        job.ItemsWritten += orders.Length;
        job.ItemsRejected += rejected;
        job.Checkpoint = checkpoint;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task CompleteAsync(
        Guid jobId,
        DateTimeOffset windowStartUtc,
        bool fullBackfill,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var job = await context.IngestionJobs.SingleAsync(item => item.Id == jobId, cancellationToken);
        job.Status = job.ItemsRejected == 0 ? IngestionJobStatus.Succeeded : IngestionJobStatus.PartiallySucceeded;
        job.CompletedUtc = timeProvider.GetUtcNow();
        job.Checkpoint = Checkpoint("complete", null, null, null, windowStartUtc, fullBackfill);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkStoppedAsync(Guid jobId, IngestionJobStatus status, string error)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(CancellationToken.None);
            var job = await context.IngestionJobs.SingleOrDefaultAsync(item => item.Id == jobId, CancellationToken.None);
            if (job is null) return;
            job.Status = status;
            job.CompletedUtc = timeProvider.GetUtcNow();
            job.ErrorSummary = Limit(error, 4000);
            await context.SaveChangesAsync(CancellationToken.None);
        }
        catch
        {
            // Preserve the original API or cancellation failure.
        }
    }

    private async Task<OrderReconciliationResult> BuildResultAsync(
        Guid jobId,
        string? error,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var job = await context.IngestionJobs.AsNoTracking().SingleAsync(item => item.Id == jobId, cancellationToken);
        var attributedInRun = await context.AffiliateOrders.AsNoTracking()
            .CountAsync(order => order.ClickId != null && order.LastSeenUtc >= job.StartedUtc, cancellationToken);
        return new OrderReconciliationResult(
            job.Id,
            job.Status,
            job.ItemsRead,
            job.ItemsWritten,
            job.ItemsRejected,
            attributedInRun,
            job.Checkpoint?.Contains("\"isFullBackfill\":true", StringComparison.Ordinal) == true,
            error ?? job.ErrorSummary);
    }

    private static void EnsureSuccess(AliExpressApiCallResult response)
    {
        if (response.IsSuccess) return;
        var detail = response.PlatformResponseMessage ?? response.HttpStatusCode.ToString();
        throw new InvalidOperationException($"AliExpress {response.Method} failed ({response.PlatformResponseCode ?? "HTTP"}): {detail}");
    }

    private static AliExpressPage<AliExpressOrder> ReadOrders(AliExpressApiCallResult response)
    {
        if (string.Equals(response.PlatformResponseCode, "405", StringComparison.Ordinal) &&
            response.PlatformResponseMessage?.Contains("result is empty", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new AliExpressPage<AliExpressOrder>([], null, null, 0);
        }

        EnsureSuccess(response);
        return AliExpressResponseReader.ReadOrders(response.RawResponse);
    }

    private static string Checkpoint(
        string phase,
        string? status,
        int? page,
        string? queryIndex,
        DateTimeOffset? windowStartUtc,
        bool isFullBackfill) =>
        JsonSerializer.Serialize(new { phase, status, page, queryIndex, windowStartUtc, isFullBackfill });

    private static bool IsValidOrderId(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 100;

    private static decimal? ParseRate(string? value)
    {
        if (!decimal.TryParse(value?.Trim().TrimEnd('%'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)) return null;
        return value?.Contains('%', StringComparison.Ordinal) == true || parsed > 1 ? parsed / 100 : parsed;
    }

    private static string? ExtractClickId(string? customParameters)
    {
        if (string.IsNullOrWhiteSpace(customParameters)) return null;
        try
        {
            using var document = JsonDocument.Parse(customParameters);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var name in new[] { "dp", "clickid", "clickId" })
            {
                if (!document.RootElement.TryGetProperty(name, out var value)) continue;
                var candidate = value.ValueKind is JsonValueKind.String or JsonValueKind.Number ? value.ToString().Trim() : string.Empty;
                return candidate.Length is > 0 and <= 64 ? candidate : null;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string? Limit(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maximumLength ? trimmed : trimmed[..maximumLength];
    }

    private static class PacificClock
    {
        private static readonly TimeZoneInfo Zone = ResolveZone();
        private static readonly string[] Formats = ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd H:mm:ss", "yyyy-MM-dd"];

        public static DateTime FromUtc(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, Zone).DateTime;

        public static DateTimeOffset? Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !DateTime.TryParseExact(value.Trim(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            {
                return null;
            }

            local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            if (Zone.IsInvalidTime(local)) local = local.AddHours(1);
            var utc = TimeZoneInfo.ConvertTimeToUtc(local, Zone);
            return new DateTimeOffset(utc);
        }

        private static TimeZoneInfo ResolveZone()
        {
            foreach (var id in new[] { "Pacific Standard Time", "America/Los_Angeles" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
            }

            throw new InvalidOperationException("A Pacific time-zone definition is required for AliExpress order timestamps.");
        }
    }
}
