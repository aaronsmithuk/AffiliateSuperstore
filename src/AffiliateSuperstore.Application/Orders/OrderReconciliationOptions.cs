namespace AffiliateSuperstore.Application.Orders;

public sealed class OrderReconciliationOptions
{
    public const string SectionName = "OrderReconciliation";

    public bool Enabled { get; set; }
    public int RefreshEveryMinutes { get; set; } = 60;
    public int FailureRetryMinutes { get; set; } = 15;
    public int InitialLookbackDays { get; set; } = 180;
    public int IncrementalLookbackHours { get; set; } = 48;
    public int FullBackfillEveryDays { get; set; } = 30;
    public int PageSize { get; set; } = 50;
    public int MaximumPagesPerStatus { get; set; } = 200;
    public int OpenOrderBatchSize { get; set; } = 50;
}

public static class OrderReconciliationPlanner
{
    public static bool IsDue(
        Persistence.Entities.IngestionJobStatus? latestStatus,
        DateTimeOffset? latestActivityUtc,
        DateTimeOffset now,
        OrderReconciliationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (latestStatus is null || latestActivityUtc is null) return true;

        var delay = latestStatus == Persistence.Entities.IngestionJobStatus.Failed
            ? TimeSpan.FromMinutes(options.FailureRetryMinutes)
            : TimeSpan.FromMinutes(options.RefreshEveryMinutes);
        return latestActivityUtc <= now - delay;
    }

    public static void Validate(OrderReconciliationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.RefreshEveryMinutes is < 5 or > 10080) throw new InvalidOperationException("OrderReconciliation:RefreshEveryMinutes must be between 5 and 10080.");
        if (options.FailureRetryMinutes is < 1 or > 1440) throw new InvalidOperationException("OrderReconciliation:FailureRetryMinutes must be between 1 and 1440.");
        if (options.InitialLookbackDays is < 1 or > 180) throw new InvalidOperationException("OrderReconciliation:InitialLookbackDays must be between 1 and 180.");
        if (options.IncrementalLookbackHours is < 1 or > 4320) throw new InvalidOperationException("OrderReconciliation:IncrementalLookbackHours must be between 1 and 4320.");
        if (options.FullBackfillEveryDays is < 1 or > 90) throw new InvalidOperationException("OrderReconciliation:FullBackfillEveryDays must be between 1 and 90.");
        if (options.PageSize is < 1 or > 50) throw new InvalidOperationException("OrderReconciliation:PageSize must be between 1 and 50.");
        if (options.MaximumPagesPerStatus is < 1 or > 1000) throw new InvalidOperationException("OrderReconciliation:MaximumPagesPerStatus must be between 1 and 1000.");
        if (options.OpenOrderBatchSize is < 1 or > 50) throw new InvalidOperationException("OrderReconciliation:OpenOrderBatchSize must be between 1 and 50.");
    }
}
