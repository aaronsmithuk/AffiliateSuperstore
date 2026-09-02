using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Orders;

public enum ConversionReadinessArea
{
    Configuration,
    CallbackSafety,
    Reconciliation,
    Attribution,
    ExternalEvidence
}

public enum ConversionReadinessState
{
    Passed,
    Warning,
    Blocked,
    NotObserved
}

public sealed record ConversionReadinessCheck(
    string Key,
    ConversionReadinessArea Area,
    string Title,
    string Evidence,
    string RequiredAction,
    ConversionReadinessState State,
    bool BlocksActivation);

public sealed record ConversionReadinessReport(
    DateTimeOffset GeneratedUtc,
    int OutboundClicks,
    int Orders,
    int AttributedOrders,
    int UnattributedOrders,
    int SettledAttributedOrders,
    int AwaitingSettlementOrders,
    int S2sEvents,
    int UnattributedS2sEvents,
    DateTimeOffset? LatestReconciliationUtc,
    DateTimeOffset? LatestFullBackfillUtc,
    IReadOnlyList<ConversionReadinessCheck> Checks)
{
    public int PassedChecks => Checks.Count(check => check.State == ConversionReadinessState.Passed);
    public int WarningChecks => Checks.Count(check => check.State == ConversionReadinessState.Warning);
    public int TechnicalBlockingChecks => Checks.Count(check =>
        check.BlocksActivation && check.Area != ConversionReadinessArea.ExternalEvidence);
    public int ExternalBlockingChecks => Checks.Count(check =>
        check.BlocksActivation && check.Area == ConversionReadinessArea.ExternalEvidence);
    public bool CanStartControlledActivation => TechnicalBlockingChecks == 0 && ExternalBlockingChecks == 0;
    public decimal OrderAttributionRate => Orders == 0 ? 0 : (decimal)AttributedOrders / Orders;
}

public sealed class ConversionReadinessService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    OrderReconciliationOptions reconciliationOptions,
    AffiliateS2sOptions s2sOptions,
    AliExpressOptions aliExpressOptions,
    ConversionEvidenceOptions evidenceOptions,
    TimeProvider timeProvider)
{
    private static readonly IReadOnlyList<EvidenceGateDefinition> ExternalEvidenceGates =
    [
        new(
            "evidence.agreement-2025",
            "agreement-2025",
            "Current Affiliate Programme agreement",
            "The complete agreement effective 1 April 2025 is not present in the evidence archive; the complete local agreement is the 2022 version.",
            "Obtain the complete 2025 agreement from AliExpress, redact and hash the capture, then compare it with the 2022 version.",
            365),
        new(
            "evidence.api-quota",
            "api-quota",
            "Application-specific API quota",
            "No authoritative per-second, per-minute or daily Affiliate API allowance is captured for application 6102.",
            "Obtain written app-specific limits and reset behaviour from AliExpress; keep serial 1,100 ms pacing until then.",
            365),
        new(
            "evidence.cache-policy",
            "cache-policy",
            "Affiliate data cache policy",
            "No authoritative cache, refresh and deletion periods are captured for product facts, prices, images or commission data.",
            "Obtain written field-specific cache and deletion rules from AliExpress.",
            365),
        new(
            "evidence.api-lifecycle",
            "api-lifecycle",
            "Supported Affiliate API lifecycle",
            "The public Affiliate API documentation is labelled deprecated, but no supported successor or shutdown timetable is captured.",
            "Obtain written successor and migration/lifetime guidance before treating the integration as durable.",
            180),
        new(
            "evidence.commission-model",
            "commission-model",
            "Current account commission classification",
            "The 30 August 2026 capture showed Non-Transparent Channels while verified-site reclassification was pending.",
            "Recheck the signed-in Commission Rules page and capture the assigned model, rates and overrides before forecasting or activation.",
            30),
        new(
            "evidence.portals-mapping",
            "portals-mapping",
            "Portals callback mapping and HTTPS",
            "The application cannot inspect the signed-in Portals S2S rule or the provider-to-production HTTPS path.",
            "Confirm every documented field mapping, dollars units, fixed parameter and the production HTTPS destination in Portals.",
            30),
        new(
            "evidence.legitimate-order",
            "legitimate-order",
            "Legitimate click-to-settlement proof",
            "Stored rows cannot prove that a callback came from an unrelated customer rather than a synthetic canary.",
            "Observe one legitimate unrelated-customer paid event and the same sub-order reaching Completed Settlement or Invalid through the signed API; do not self-purchase.",
            365)
    ];

    private static readonly EvidenceGateDefinition SyntheticCanaryGate = new(
        "callback.synthetic-canary",
        "synthetic-canary",
        "Controlled callback canary",
        "Synthetic canary completion is intentionally not persisted after its test rows are removed.",
        "Run the documented wrong-token, accepted-event and duplicate-delivery checks in a reviewed change window.",
        30);

    public async Task<ConversionReadinessReport> GetAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var latestJob = await context.IngestionJobs.AsNoTracking()
            .Where(job => job.Type == IngestionJobType.OrderReconciliation)
            .OrderByDescending(job => job.QueuedUtc)
            .Select(job => new ReconciliationJobRow(job.Status, job.StartedUtc, job.CompletedUtc))
            .FirstOrDefaultAsync(cancellationToken);
        var latestFullBackfillUtc = await context.IngestionJobs.AsNoTracking()
            .Where(job =>
                job.Type == IngestionJobType.OrderReconciliation &&
                job.Status == IngestionJobStatus.Succeeded &&
                job.CompletedUtc != null &&
                job.Checkpoint != null &&
                job.Checkpoint.Contains("\"isFullBackfill\":true"))
            .OrderByDescending(job => job.CompletedUtc)
            .Select(job => job.CompletedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var outboundClicks = await context.OutboundClicks.AsNoTracking().CountAsync(cancellationToken);
        var orders = await context.AffiliateOrders.AsNoTracking().CountAsync(cancellationToken);
        var attributedOrders = await context.AffiliateOrders.AsNoTracking()
            .CountAsync(order => order.ClickId != null, cancellationToken);
        var settledAttributedOrders = await context.AffiliateOrders.AsNoTracking()
            .CountAsync(order =>
                order.ClickId != null &&
                order.Status == AliExpressOrderStatuses.CompletedSettlement,
                cancellationToken);
        var awaitingSettlementOrders = await context.AffiliateOrders.AsNoTracking()
            .CountAsync(order =>
                order.Status != AliExpressOrderStatuses.CompletedSettlement &&
                order.Status != AliExpressOrderStatuses.Invalid,
                cancellationToken);
        var s2sEvents = await context.AffiliateS2sEvents.AsNoTracking().CountAsync(cancellationToken);
        var unattributedS2sEvents = await context.AffiliateS2sEvents.AsNoTracking()
            .CountAsync(item => item.ClickId == null, cancellationToken);
        var unattributedOrders = orders - attributedOrders;
        var latestReconciliationUtc = latestJob?.CompletedUtc ?? latestJob?.StartedUtc;

        var checks = BuildTechnicalChecks(
            now,
            latestJob,
            latestFullBackfillUtc,
            outboundClicks,
            orders,
            unattributedOrders,
            settledAttributedOrders,
            awaitingSettlementOrders,
            s2sEvents,
            unattributedS2sEvents);
        checks.AddRange(ExternalEvidenceGates.Select(gate => EvidenceCheck(gate, now, ConversionReadinessArea.ExternalEvidence)));

        return new ConversionReadinessReport(
            now,
            outboundClicks,
            orders,
            attributedOrders,
            unattributedOrders,
            settledAttributedOrders,
            awaitingSettlementOrders,
            s2sEvents,
            unattributedS2sEvents,
            latestReconciliationUtc,
            latestFullBackfillUtc,
            checks);
    }

    private List<ConversionReadinessCheck> BuildTechnicalChecks(
        DateTimeOffset now,
        ReconciliationJobRow? latestJob,
        DateTimeOffset? latestFullBackfillUtc,
        int outboundClicks,
        int orders,
        int unattributedOrders,
        int settledAttributedOrders,
        int awaitingSettlementOrders,
        int s2sEvents,
        int unattributedS2sEvents)
    {
        var checks = new List<ConversionReadinessCheck>
        {
            aliExpressOptions.IsConfigured
                ? Passed(
                    "configuration.api-credentials",
                    ConversionReadinessArea.Configuration,
                    "Signed Affiliate API credentials",
                    "The AppKey and App Secret are configured; their values are never displayed.",
                    "Keep both values in protected hosting configuration and validate them with a signed smoke request.")
                : Blocked(
                    "configuration.api-credentials",
                    ConversionReadinessArea.Configuration,
                    "Signed Affiliate API credentials",
                    "The AppKey or App Secret required for authoritative reconciliation is missing.",
                    "Configure both values in protected hosting configuration before running reconciliation."),
            IsApiTransportReady()
                ? Passed(
                    "configuration.api-https",
                    ConversionReadinessArea.Configuration,
                    "HTTPS provider gateways",
                    "Both configured AliExpress gateways use HTTPS.",
                    "Keep signed Affiliate API traffic on the approved HTTPS gateways.")
                : Blocked(
                    "configuration.api-https",
                    ConversionReadinessArea.Configuration,
                    "HTTPS provider gateways",
                    "One or more configured AliExpress gateways do not use HTTPS.",
                    "Restore the approved HTTPS Affiliate API gateways before any signed request."),
            aliExpressOptions.MinimumRequestIntervalMilliseconds >= 1100
                ? Passed(
                    "configuration.api-pacing",
                    ConversionReadinessArea.Configuration,
                    "Conservative API pacing",
                    "Process-wide request pacing is at least the observed safe 1,100 ms interval.",
                    "Retain pacing until AliExpress supplies authoritative app-specific quota evidence.")
                : Blocked(
                    "configuration.api-pacing",
                    ConversionReadinessArea.Configuration,
                    "Conservative API pacing",
                    "Request pacing is below the 1,100 ms interval adopted after the live ApiCallLimit response.",
                    "Restore at least 1,100 ms process-wide pacing until written quota evidence supports a change."),
            s2sOptions.Enabled
                ? Blocked(
                    "configuration.s2s-disabled",
                    ConversionReadinessArea.Configuration,
                    "S2S remains disabled during preflight",
                    "S2S is currently enabled; the preflight cannot establish whether Portals and the live callback have been approved.",
                    "Disable S2S until every technical, manual and external gate has been reviewed.")
                : Passed(
                    "configuration.s2s-disabled",
                    ConversionReadinessArea.Configuration,
                    "S2S remains disabled during preflight",
                    "The callback is safely disabled and returns 404.",
                    "Keep it disabled until controlled activation is explicitly approved."),
            IsVerificationTokenReady()
                ? Passed(
                    "configuration.s2s-token",
                    ConversionReadinessArea.CallbackSafety,
                    "Fixed verification token",
                    "A token is configured and meets the 32–512 character length contract; its value is never displayed.",
                    "Protect it from logs and rotate it after suspected exposure.")
                : Blocked(
                    "configuration.s2s-token",
                    ConversionReadinessArea.CallbackSafety,
                    "Fixed verification token",
                    "No production-shaped 32–512 character fixed token is configured.",
                    "Generate a unique random secret in protected hosting configuration while leaving S2S disabled."),
            IsPayloadLimitReady()
                ? Passed(
                    "configuration.payload-limit",
                    ConversionReadinessArea.CallbackSafety,
                    "Allow-listed payload bound",
                    "The configured payload bound is inside the enforced 512–65,536 character range.",
                    "Retain the bounded allow-list and no-store response controls.")
                : Blocked(
                    "configuration.payload-limit",
                    ConversionReadinessArea.CallbackSafety,
                    "Allow-listed payload bound",
                    "The callback payload bound is outside the enforced safe range.",
                    "Correct AliExpressS2s:MaximumPayloadCharacters before any callback test."),
            reconciliationOptions.Enabled
                ? Passed(
                    "configuration.reconciliation-enabled",
                    ConversionReadinessArea.Configuration,
                    "Automatic signed-API reconciliation",
                    "Automatic reconciliation is enabled; signed API state remains authoritative over S2S estimates.",
                    "Keep reconciliation enabled through activation and rollback.")
                : Blocked(
                    "configuration.reconciliation-enabled",
                    ConversionReadinessArea.Configuration,
                    "Automatic signed-API reconciliation",
                    "Automatic reconciliation is disabled.",
                    "Enable and validate signed reconciliation before enabling S2S."),
            ReconciliationOptionsCheck()
        };

        checks.Add(ReconciliationRecoveryPolicyCheck());

        checks.Add(LatestReconciliationCheck(now, latestJob));
        checks.Add(FullBackfillCheck(now, latestFullBackfillUtc));
        checks.Add(outboundClicks > 0
            ? Passed(
                "attribution.outbound-clicks",
                ConversionReadinessArea.Attribution,
                "Outbound click evidence",
                $"{outboundClicks:N0} opaque outbound click record(s) are stored.",
                "Confirm new clicks continue to carry dp without customer identity.")
            : NotObserved(
                "attribution.outbound-clicks",
                ConversionReadinessArea.Attribution,
                "Outbound click evidence",
                "No outbound click record exists, so dp-to-order attribution cannot be exercised.",
                "Generate a normal customer-facing affiliate hand-off before controlled live validation.",
                blocksActivation: true));
        checks.Add(orders == 0
            ? NotObserved(
                "attribution.order-match",
                ConversionReadinessArea.Attribution,
                "Order attribution gaps",
                "No orders are stored, so attribution quality has not been observed.",
                "Wait for a legitimate event; an empty signed-API scan proves health only.",
                blocksActivation: false)
            : unattributedOrders == 0
                ? Passed(
                    "attribution.order-match",
                    ConversionReadinessArea.Attribution,
                    "Order attribution gaps",
                    $"All {orders:N0} stored order(s) are joined to an outbound click.",
                    "Continue reviewing missing dp/clickid counts daily.")
                : Blocked(
                    "attribution.order-match",
                    ConversionReadinessArea.Attribution,
                    "Order attribution gaps",
                    $"{unattributedOrders:N0} of {orders:N0} stored order(s) have no recognised click ID.",
                    "Correct the Portals dp → clickid mapping or investigate the affected sub-orders before activation."));
        checks.Add(s2sEvents == 0
            ? NotObserved(
                "attribution.s2s-match",
                ConversionReadinessArea.Attribution,
                "S2S attribution gaps",
                "No S2S event is stored; callback attribution has not been observed.",
                "Use the controlled synthetic canary, remove its rows, then wait for a legitimate event.",
                blocksActivation: false)
            : unattributedS2sEvents == 0
                ? Passed(
                    "attribution.s2s-match",
                    ConversionReadinessArea.Attribution,
                    "S2S attribution gaps",
                    $"All {s2sEvents:N0} stored S2S event(s) carry a recognised click ID.",
                    "Compare them with signed-API and Portals order evidence.")
                : Blocked(
                    "attribution.s2s-match",
                    ConversionReadinessArea.Attribution,
                    "S2S attribution gaps",
                    $"{unattributedS2sEvents:N0} of {s2sEvents:N0} stored S2S event(s) have no recognised click ID.",
                    "Verify dp → clickid mapping and investigate each unmatched sub-order."));
        checks.Add(settledAttributedOrders > 0
            ? Passed(
                "reconciliation.settled-attribution",
                ConversionReadinessArea.Reconciliation,
                "Attributed terminal settlement",
                $"{settledAttributedOrders:N0} attributed order(s) reached Completed Settlement.",
                "Retain the order, click and job evidence for audit.")
            : NotObserved(
                "reconciliation.settled-attribution",
                ConversionReadinessArea.Reconciliation,
                "Attributed terminal settlement",
                "No attributed order has reached Completed Settlement in local evidence.",
                "Observe a legitimate paid sub-order through signed-API Completed Settlement or Invalid.",
                blocksActivation: true));
        checks.Add(awaitingSettlementOrders == 0
            ? Passed(
                "reconciliation.awaiting-settlement",
                ConversionReadinessArea.Reconciliation,
                "Orders awaiting terminal state",
                "No stored order is currently awaiting Completed Settlement or Invalid.",
                "Continue refreshing every known non-terminal sub-order.")
            : Warning(
                "reconciliation.awaiting-settlement",
                ConversionReadinessArea.Reconciliation,
                "Orders awaiting terminal state",
                $"{awaitingSettlementOrders:N0} stored order(s) still have an estimated, non-terminal state.",
                "Keep reconciliation running; do not report their estimated commission as settled revenue.",
                blocksActivation: false));
        checks.Add(EvidenceCheck(SyntheticCanaryGate, now, ConversionReadinessArea.CallbackSafety));

        return checks;
    }

    private ConversionReadinessCheck ReconciliationOptionsCheck()
    {
        try
        {
            OrderReconciliationPlanner.Validate(reconciliationOptions);
            return Passed(
                "configuration.reconciliation-options",
                ConversionReadinessArea.Configuration,
                "Reconciliation safety bounds",
                "Lookback, overlap, backfill, page and batch settings are inside their validated ranges.",
                "Retain the 180-day recovery and 48-hour overlap policy unless provider evidence changes.");
        }
        catch (InvalidOperationException)
        {
            return Blocked(
                "configuration.reconciliation-options",
                ConversionReadinessArea.Configuration,
                "Reconciliation safety bounds",
                "One or more reconciliation settings are outside their validated range.",
                "Correct the protected reconciliation configuration and complete a successful full backfill.");
        }
    }

    private ConversionReadinessCheck ReconciliationRecoveryPolicyCheck()
    {
        if (reconciliationOptions.InitialLookbackDays == 180 &&
            reconciliationOptions.IncrementalLookbackHours >= 48 &&
            reconciliationOptions.FullBackfillEveryDays <= 30)
        {
            return Passed(
                "configuration.reconciliation-recovery-policy",
                ConversionReadinessArea.Configuration,
                "Order recovery policy",
                "Initial recovery covers 180 days, incremental discovery overlaps by at least 48 hours and full recovery runs at least monthly.",
                "Retain these bounds while the provider query window remains 180 days.");
        }

        return Blocked(
            "configuration.reconciliation-recovery-policy",
            ConversionReadinessArea.Configuration,
            "Order recovery policy",
            "The configured lookback, overlap or full-backfill interval does not meet the 180-day / 48-hour / monthly recovery policy.",
            "Restore the documented recovery policy and complete a fresh full backfill.");
    }

    private ConversionReadinessCheck LatestReconciliationCheck(DateTimeOffset now, ReconciliationJobRow? latestJob)
    {
        if (latestJob is null)
        {
            return NotObserved(
                "reconciliation.latest-run",
                ConversionReadinessArea.Reconciliation,
                "Latest signed reconciliation",
                "No reconciliation job has run.",
                "Run a signed full backfill and confirm a successful completion.",
                blocksActivation: true);
        }

        var activityUtc = latestJob.CompletedUtc ?? latestJob.StartedUtc;
        if (latestJob.Status != IngestionJobStatus.Succeeded || activityUtc is null)
        {
            return Blocked(
                "reconciliation.latest-run",
                ConversionReadinessArea.Reconciliation,
                "Latest signed reconciliation",
                $"The latest reconciliation state is {latestJob.Status}; it is not a completed success.",
                "Resolve the failure or incomplete run and complete a fresh signed reconciliation.");
        }

        var maximumAge = TimeSpan.FromMinutes(reconciliationOptions.RefreshEveryMinutes * 2d);
        var age = now - activityUtc.Value;
        if (age > maximumAge)
        {
            return Blocked(
                "reconciliation.latest-run",
                ConversionReadinessArea.Reconciliation,
                "Latest signed reconciliation",
                $"The latest successful reconciliation is {FormatAge(age)} old, beyond two configured refresh intervals.",
                "Run reconciliation and diagnose the worker schedule before activation.");
        }

        return Passed(
            "reconciliation.latest-run",
            ConversionReadinessArea.Reconciliation,
            "Latest signed reconciliation",
            $"The latest signed reconciliation succeeded {FormatAge(age)} ago.",
            "Confirm it remains fresh throughout controlled activation.");
    }

    private ConversionReadinessCheck FullBackfillCheck(DateTimeOffset now, DateTimeOffset? latestFullBackfillUtc)
    {
        if (latestFullBackfillUtc is null)
        {
            return NotObserved(
                "reconciliation.full-backfill",
                ConversionReadinessArea.Reconciliation,
                "180-day recovery scan",
                "No successful full-backfill checkpoint is stored.",
                "Complete a signed 180-day full backfill before activation.",
                blocksActivation: true);
        }

        var age = now - latestFullBackfillUtc.Value;
        if (age > TimeSpan.FromDays(reconciliationOptions.FullBackfillEveryDays))
        {
            return Blocked(
                "reconciliation.full-backfill",
                ConversionReadinessArea.Reconciliation,
                "180-day recovery scan",
                $"The latest successful full backfill is {FormatAge(age)} old and exceeds the configured recovery interval.",
                "Complete a new 180-day recovery scan before activation.");
        }

        return Passed(
            "reconciliation.full-backfill",
            ConversionReadinessArea.Reconciliation,
            "180-day recovery scan",
            $"A successful full backfill completed {FormatAge(age)} ago.",
            "Retain monthly recovery scans beyond the provider query window.");
    }

    private bool IsVerificationTokenReady() =>
        !string.IsNullOrWhiteSpace(s2sOptions.VerificationToken) &&
        s2sOptions.VerificationToken.Length is >= AffiliateS2sOptions.MinimumVerificationTokenCharacters and <= AffiliateS2sOptions.MaximumVerificationTokenCharacters;

    private bool IsPayloadLimitReady() =>
        s2sOptions.MaximumPayloadCharacters is >= AffiliateS2sOptions.MinimumPayloadCharacters and <= AffiliateS2sOptions.MaximumPayloadCharactersLimit;

    private bool IsApiTransportReady() =>
        string.Equals(aliExpressOptions.Gateway.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(aliExpressOptions.SystemGateway.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private ConversionReadinessCheck EvidenceCheck(
        EvidenceGateDefinition gate,
        DateTimeOffset now,
        ConversionReadinessArea area)
    {
        if (evidenceOptions.Acknowledgements is null ||
            !evidenceOptions.Acknowledgements.TryGetValue(gate.ConfigurationKey, out var acknowledgement) ||
            acknowledgement is null ||
            !acknowledgement.Confirmed)
        {
            return new ConversionReadinessCheck(
                gate.CheckKey,
                area,
                gate.Title,
                gate.UnconfirmedEvidence,
                AcknowledgementAction(gate),
                ConversionReadinessState.Blocked,
                true);
        }

        if (!IsBoundedAuditText(acknowledgement.EvidenceReference, 8, 500) ||
            !IsBoundedAuditText(acknowledgement.ConfirmedBy, 2, 200) ||
            acknowledgement.ConfirmedUtc is null ||
            acknowledgement.ConfirmedUtc.Value.Offset != TimeSpan.Zero)
        {
            return new ConversionReadinessCheck(
                gate.CheckKey,
                area,
                gate.Title,
                "The acknowledgement is malformed or its timestamp is not UTC-normalised.",
                $"Correct exact key {gate.ConfigurationKey}: set Confirmed=true with a bounded non-secret reference, confirmer and UTC timestamp ending in Z. {gate.RequiredAction}",
                ConversionReadinessState.Blocked,
                true);
        }

        var confirmedUtc = acknowledgement.ConfirmedUtc.Value;
        if (confirmedUtc > now.AddMinutes(5))
        {
            return new ConversionReadinessCheck(
                gate.CheckKey,
                area,
                gate.Title,
                "The acknowledgement timestamp is in the future.",
                $"Correct the protected UTC timestamp for exact key {gate.ConfigurationKey}; future-dated evidence cannot clear a gate.",
                ConversionReadinessState.Blocked,
                true);
        }

        var age = now - confirmedUtc;
        if (age > TimeSpan.FromDays(gate.MaximumAgeDays))
        {
            return new ConversionReadinessCheck(
                gate.CheckKey,
                area,
                gate.Title,
                $"The acknowledgement expired after its {gate.MaximumAgeDays}-day review interval.",
                $"Recapture and review the source, then replace exact key {gate.ConfigurationKey}. {gate.RequiredAction}",
                ConversionReadinessState.Blocked,
                true);
        }

        return new ConversionReadinessCheck(
            gate.CheckKey,
            area,
            gate.Title,
            $"Confirmed {confirmedUtc:yyyy-MM-dd HH:mm} UTC by {acknowledgement.ConfirmedBy}; evidence reference: {acknowledgement.EvidenceReference}.",
            $"Reconfirm within {gate.MaximumAgeDays} days or revoke immediately if the evidence changes.",
            ConversionReadinessState.Passed,
            false);
    }

    private static bool IsBoundedAuditText(string? value, int minimumLength, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length >= minimumLength &&
        value.Length <= maximumLength &&
        !value.Any(char.IsControl);

    private static string AcknowledgementAction(EvidenceGateDefinition gate) =>
        $"{gate.RequiredAction} Then acknowledge exact key {gate.ConfigurationKey} in protected configuration.";

    private static string FormatAge(TimeSpan age)
    {
        if (age < TimeSpan.Zero) return "less than a minute";
        if (age < TimeSpan.FromHours(1)) return $"{Math.Max(1, (int)age.TotalMinutes)} minute(s)";
        if (age < TimeSpan.FromDays(2)) return $"{(int)age.TotalHours} hour(s)";
        return $"{(int)age.TotalDays} day(s)";
    }

    private static ConversionReadinessCheck Passed(
        string key,
        ConversionReadinessArea area,
        string title,
        string evidence,
        string action) =>
        new(key, area, title, evidence, action, ConversionReadinessState.Passed, false);

    private static ConversionReadinessCheck Warning(
        string key,
        ConversionReadinessArea area,
        string title,
        string evidence,
        string action,
        bool blocksActivation) =>
        new(key, area, title, evidence, action, ConversionReadinessState.Warning, blocksActivation);

    private static ConversionReadinessCheck Blocked(
        string key,
        ConversionReadinessArea area,
        string title,
        string evidence,
        string action) =>
        new(key, area, title, evidence, action, ConversionReadinessState.Blocked, true);

    private static ConversionReadinessCheck NotObserved(
        string key,
        ConversionReadinessArea area,
        string title,
        string evidence,
        string action,
        bool blocksActivation) =>
        new(key, area, title, evidence, action, ConversionReadinessState.NotObserved, blocksActivation);

    private sealed record ReconciliationJobRow(
        IngestionJobStatus Status,
        DateTimeOffset? StartedUtc,
        DateTimeOffset? CompletedUtc);

    private sealed record EvidenceGateDefinition(
        string CheckKey,
        string ConfigurationKey,
        string Title,
        string UnconfirmedEvidence,
        string RequiredAction,
        int MaximumAgeDays);
}
