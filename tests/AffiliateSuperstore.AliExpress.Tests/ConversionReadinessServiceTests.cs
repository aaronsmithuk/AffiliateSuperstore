using AffiliateSuperstore.Application.Orders;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class ConversionReadinessServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 14, 0, 0, TimeSpan.Zero);
    private const string VerificationToken = "production-shaped-test-token-never-report-this";

    [Fact]
    public async Task GetAsync_ReportsHealthyTechnicalEvidenceWithoutInferringManualOrExternalGates()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedHealthyEvidenceAsync(factory);
        var service = CreateService(factory, HealthyReconciliationOptions(), new AffiliateS2sOptions
        {
            Enabled = false,
            VerificationToken = VerificationToken,
            MaximumPayloadCharacters = 8192
        });

        var report = await service.GetAsync();

        Assert.Equal(1, report.OutboundClicks);
        Assert.Equal(1, report.Orders);
        Assert.Equal(1, report.AttributedOrders);
        Assert.Equal(0, report.UnattributedOrders);
        Assert.Equal(1, report.SettledAttributedOrders);
        Assert.Equal(0, report.AwaitingSettlementOrders);
        Assert.Equal(1, report.S2sEvents);
        Assert.Equal(0, report.UnattributedS2sEvents);
        Assert.Equal(1m, report.OrderAttributionRate);
        Assert.Equal(Now.AddMinutes(-10), report.LatestReconciliationUtc);
        Assert.Equal(Now.AddDays(-1), report.LatestFullBackfillUtc);
        Assert.Equal(1, report.TechnicalBlockingChecks);
        Assert.Equal(7, report.ExternalBlockingChecks);
        Assert.False(report.CanStartControlledActivation);
        AssertCheck(report, "configuration.api-credentials", ConversionReadinessState.Passed, false);
        AssertCheck(report, "configuration.api-https", ConversionReadinessState.Passed, false);
        AssertCheck(report, "configuration.api-pacing", ConversionReadinessState.Passed, false);
        AssertCheck(report, "configuration.s2s-disabled", ConversionReadinessState.Passed, false);
        AssertCheck(report, "configuration.s2s-token", ConversionReadinessState.Passed, false);
        AssertCheck(report, "configuration.reconciliation-recovery-policy", ConversionReadinessState.Passed, false);
        AssertCheck(report, "reconciliation.latest-run", ConversionReadinessState.Passed, false);
        AssertCheck(report, "reconciliation.full-backfill", ConversionReadinessState.Passed, false);
        AssertCheck(report, "reconciliation.settled-attribution", ConversionReadinessState.Passed, false);
        AssertCheck(report, "callback.synthetic-canary", ConversionReadinessState.Blocked, true);
        Assert.All(
            report.Checks,
            check => Assert.DoesNotContain(VerificationToken, check.Evidence + check.RequiredAction, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetAsync_FlagsUnsafeConfigurationStaleFailureAndAttributionGaps()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await using (var context = factory.CreateDbContext())
        {
            context.IngestionJobs.AddRange(
                Job(IngestionJobStatus.Succeeded, Now.AddDays(-31), fullBackfill: true),
                Job(IngestionJobStatus.Failed, Now.AddHours(-3), fullBackfill: false));
            context.AffiliateOrders.Add(new AffiliateOrderRecord
            {
                SubOrderId = "unmatched-order",
                Status = AliExpressOrderStatuses.PaymentCompleted,
                PaidUtc = Now.AddHours(-2),
                FirstSeenUtc = Now.AddHours(-2),
                LastSeenUtc = Now.AddHours(-1)
            });
            context.AffiliateS2sEvents.Add(new AffiliateS2sEventRecord
            {
                Id = Guid.CreateVersion7(),
                EventKey = "unmatched-event",
                SubOrderId = "unmatched-order",
                ReceivedUtc = Now.AddHours(-2),
                ProcessedUtc = Now.AddHours(-2),
                PayloadJson = "{}"
            });
            await context.SaveChangesAsync();
        }

        var reconciliationOptions = HealthyReconciliationOptions();
        reconciliationOptions.Enabled = false;
        reconciliationOptions.PageSize = 51;
        reconciliationOptions.InitialLookbackDays = 90;
        var service = CreateService(factory, reconciliationOptions, new AffiliateS2sOptions
        {
            Enabled = true,
            VerificationToken = "weak",
            MaximumPayloadCharacters = 100
        }, new AliExpressOptions
        {
            Gateway = new Uri("http://provider.invalid/sync"),
            SystemGateway = new Uri("http://provider.invalid/rest"),
            MinimumRequestIntervalMilliseconds = 0
        });

        var report = await service.GetAsync();

        Assert.True(report.TechnicalBlockingChecks >= 9);
        Assert.Equal(1, report.UnattributedOrders);
        Assert.Equal(1, report.UnattributedS2sEvents);
        Assert.Equal(1, report.AwaitingSettlementOrders);
        AssertCheck(report, "configuration.api-credentials", ConversionReadinessState.Blocked, true);
        AssertCheck(report, "configuration.api-https", ConversionReadinessState.Blocked, true);
        AssertCheck(report, "configuration.api-pacing", ConversionReadinessState.Blocked, true);
        AssertCheck(report, "configuration.s2s-disabled", ConversionReadinessState.Blocked, true);
        AssertCheck(report, "configuration.s2s-token", ConversionReadinessState.Blocked, true);
        AssertCheck(report, "configuration.payload-limit", ConversionReadinessState.Blocked, true);
        AssertCheck(report, "configuration.reconciliation-enabled", ConversionReadinessState.Blocked, true);
        AssertCheck(report, "configuration.reconciliation-options", ConversionReadinessState.Blocked, true);
        AssertCheck(report, "configuration.reconciliation-recovery-policy", ConversionReadinessState.Blocked, true);
        AssertCheck(report, "reconciliation.latest-run", ConversionReadinessState.Blocked, true);
        AssertCheck(report, "reconciliation.full-backfill", ConversionReadinessState.Blocked, true);
        AssertCheck(report, "attribution.outbound-clicks", ConversionReadinessState.NotObserved, true);
        AssertCheck(report, "attribution.order-match", ConversionReadinessState.Blocked, true);
        AssertCheck(report, "attribution.s2s-match", ConversionReadinessState.Blocked, true);
        AssertCheck(report, "reconciliation.settled-attribution", ConversionReadinessState.NotObserved, true);
        AssertCheck(report, "reconciliation.awaiting-settlement", ConversionReadinessState.Warning, false);
    }

    [Fact]
    public async Task GetAsync_RequiresObservedReconciliationAndSettlementWhenDatabaseIsEmpty()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        var service = CreateService(factory, HealthyReconciliationOptions(), new AffiliateS2sOptions
        {
            VerificationToken = VerificationToken
        });

        var report = await service.GetAsync();

        AssertCheck(report, "reconciliation.latest-run", ConversionReadinessState.NotObserved, true);
        AssertCheck(report, "reconciliation.full-backfill", ConversionReadinessState.NotObserved, true);
        AssertCheck(report, "reconciliation.settled-attribution", ConversionReadinessState.NotObserved, true);
        AssertCheck(report, "attribution.order-match", ConversionReadinessState.NotObserved, false);
        AssertCheck(report, "attribution.s2s-match", ConversionReadinessState.NotObserved, false);
        Assert.False(report.CanStartControlledActivation);
    }

    [Fact]
    public async Task GetAsync_AllowsValidCurrentAcknowledgementsButKeepsSettlementEvidenceIndependent()
    {
        var acknowledgedEvidence = AllAcknowledgements(Now.AddDays(-1));
        var healthyFactory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await SeedHealthyEvidenceAsync(healthyFactory);

        var readyService = CreateService(
            healthyFactory,
            HealthyReconciliationOptions(),
            new AffiliateS2sOptions { VerificationToken = VerificationToken },
            evidenceOptions: acknowledgedEvidence);
        var ready = await readyService.GetAsync();

        Assert.Equal(0, ready.TechnicalBlockingChecks);
        Assert.Equal(0, ready.ExternalBlockingChecks);
        Assert.True(ready.CanStartControlledActivation);
        AssertCheck(ready, "callback.synthetic-canary", ConversionReadinessState.Passed, false);
        AssertCheck(ready, "evidence.legitimate-order", ConversionReadinessState.Passed, false);
        Assert.All(
            ready.Checks.Where(check => check.Area == ConversionReadinessArea.ExternalEvidence),
            check => Assert.Contains("evidence reference:", check.Evidence, StringComparison.Ordinal));

        acknowledgedEvidence.Acknowledgements["synthetic-canary"].Confirmed = false;
        var revoked = await readyService.GetAsync();
        AssertCheck(revoked, "callback.synthetic-canary", ConversionReadinessState.Blocked, true);
        Assert.False(revoked.CanStartControlledActivation);
        acknowledgedEvidence.Acknowledgements["synthetic-canary"].Confirmed = true;

        var emptyFactory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        var withoutSettlement = await CreateService(
            emptyFactory,
            HealthyReconciliationOptions(),
            new AffiliateS2sOptions { VerificationToken = VerificationToken },
            evidenceOptions: acknowledgedEvidence).GetAsync();

        AssertCheck(withoutSettlement, "evidence.legitimate-order", ConversionReadinessState.Passed, false);
        AssertCheck(withoutSettlement, "reconciliation.settled-attribution", ConversionReadinessState.NotObserved, true);
        Assert.False(withoutSettlement.CanStartControlledActivation);
    }

    [Fact]
    public async Task GetAsync_FailsClosedForMalformedFutureExpiredRevokedAndUnknownAcknowledgements()
    {
        var cases = new[]
        {
            new EvidenceCase("malformed", new ConversionEvidenceAcknowledgement
            {
                Confirmed = true,
                EvidenceReference = "short",
                ConfirmedBy = "operator",
                ConfirmedUtc = Now.AddDays(-1)
            }, "malformed"),
            new EvidenceCase("non-utc", ValidAcknowledgement(Now.AddDays(-1).ToOffset(TimeSpan.FromHours(1))), "UTC-normalised"),
            new EvidenceCase("future", ValidAcknowledgement(Now.AddMinutes(10)), "future"),
            new EvidenceCase("expired", ValidAcknowledgement(Now.AddDays(-366)), "expired"),
            new EvidenceCase("revoked", new ConversionEvidenceAcknowledgement
            {
                Confirmed = false,
                EvidenceReference = "provenance:agreement-2025",
                ConfirmedBy = "operator",
                ConfirmedUtc = Now.AddDays(-1)
            }, "not present"),
        };

        foreach (var testCase in cases)
        {
            var options = new ConversionEvidenceOptions();
            options.Acknowledgements["agreement-2025"] = testCase.Acknowledgement;
            options.Acknowledgements["misspelled-agreement-key"] = ValidAcknowledgement(Now.AddDays(-1));
            var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));

            var report = await CreateService(
                factory,
                HealthyReconciliationOptions(),
                new AffiliateS2sOptions { VerificationToken = VerificationToken },
                evidenceOptions: options).GetAsync();

            var agreement = Assert.Single(report.Checks, check => check.Key == "evidence.agreement-2025");
            Assert.Equal(ConversionReadinessState.Blocked, agreement.State);
            Assert.True(agreement.BlocksActivation);
            Assert.Contains(testCase.ExpectedText, agreement.Evidence, StringComparison.OrdinalIgnoreCase);
        }

        var unknownOnly = new ConversionEvidenceOptions();
        unknownOnly.Acknowledgements["agreement-2025-typo"] = ValidAcknowledgement(Now.AddDays(-1));
        var unknownReport = await CreateService(
            new InMemoryFactory(Guid.NewGuid().ToString("N")),
            HealthyReconciliationOptions(),
            new AffiliateS2sOptions { VerificationToken = VerificationToken },
            evidenceOptions: unknownOnly).GetAsync();
        AssertCheck(unknownReport, "evidence.agreement-2025", ConversionReadinessState.Blocked, true);
    }

    private static ConversionReadinessService CreateService(
        IDbContextFactory<AffiliateSuperstoreDbContext> factory,
        OrderReconciliationOptions reconciliationOptions,
        AffiliateS2sOptions s2sOptions,
        AliExpressOptions? aliExpressOptions = null,
        ConversionEvidenceOptions? evidenceOptions = null) =>
        new(
            factory,
            reconciliationOptions,
            s2sOptions,
            aliExpressOptions ?? HealthyAliExpressOptions(),
            evidenceOptions ?? new ConversionEvidenceOptions(),
            new FixedTimeProvider(Now));

    private static ConversionEvidenceOptions AllAcknowledgements(DateTimeOffset confirmedUtc)
    {
        var options = new ConversionEvidenceOptions();
        foreach (var key in new[]
        {
            "agreement-2025",
            "api-quota",
            "cache-policy",
            "api-lifecycle",
            "commission-model",
            "portals-mapping",
            "legitimate-order",
            "synthetic-canary"
        })
        {
            options.Acknowledgements[key] = ValidAcknowledgement(confirmedUtc);
        }

        return options;
    }

    private static ConversionEvidenceAcknowledgement ValidAcknowledgement(DateTimeOffset confirmedUtc) => new()
    {
        Confirmed = true,
        EvidenceReference = "provenance:sha256-0123456789abcdef",
        ConfirmedBy = "conversion-operator",
        ConfirmedUtc = confirmedUtc
    };

    private static AliExpressOptions HealthyAliExpressOptions() => new()
    {
        AppKey = "configured-app-key",
        AppSecret = "configured-app-secret",
        MinimumRequestIntervalMilliseconds = 1100
    };

    private static OrderReconciliationOptions HealthyReconciliationOptions() => new()
    {
        Enabled = true,
        RefreshEveryMinutes = 60,
        FailureRetryMinutes = 15,
        InitialLookbackDays = 180,
        IncrementalLookbackHours = 48,
        FullBackfillEveryDays = 30,
        PageSize = 50,
        MaximumPagesPerStatus = 200,
        OpenOrderBatchSize = 50
    };

    private static async Task SeedHealthyEvidenceAsync(InMemoryFactory factory)
    {
        await using var context = factory.CreateDbContext();
        var shopId = Guid.CreateVersion7();
        context.Shops.Add(new ShopRecord
        {
            Id = shopId,
            Slug = "plushies",
            DisplayName = "The Plushy Shop",
            PathPrefix = "/plushies",
            TrackingId = "theplushyshop",
            DefaultSearchQuery = "plush toy",
            SeoTitle = "Plush toys",
            SeoDescription = "Curated plush toys",
            PrimaryColour = "#000000",
            AccentColour = "#ffffff",
            CreatedUtc = Now.AddDays(-10),
            UpdatedUtc = Now
        });
        context.OutboundClicks.Add(new OutboundClickRecord
        {
            ClickId = "settled-click",
            ShopId = shopId,
            TrackingId = "theplushyshop",
            Campaign = "plushies",
            Placement = "product-page",
            ClickedUtc = Now.AddDays(-2),
            ConvertedUtc = Now.AddDays(-1)
        });
        context.AffiliateOrders.Add(new AffiliateOrderRecord
        {
            SubOrderId = "settled-order",
            ClickId = "settled-click",
            Status = AliExpressOrderStatuses.CompletedSettlement,
            PaidUtc = Now.AddDays(-1),
            CompletedSettlementUtc = Now.AddHours(-12),
            FirstSeenUtc = Now.AddDays(-1),
            LastSeenUtc = Now.AddMinutes(-10)
        });
        context.AffiliateS2sEvents.Add(new AffiliateS2sEventRecord
        {
            Id = Guid.CreateVersion7(),
            EventKey = "settled-event",
            SubOrderId = "settled-order",
            ClickId = "settled-click",
            ReceivedUtc = Now.AddDays(-1),
            ProcessedUtc = Now.AddDays(-1),
            PayloadJson = "{}"
        });
        context.IngestionJobs.AddRange(
            Job(IngestionJobStatus.Succeeded, Now.AddDays(-1), fullBackfill: true),
            Job(IngestionJobStatus.Succeeded, Now.AddMinutes(-10), fullBackfill: false));
        await context.SaveChangesAsync();
    }

    private static IngestionJobRecord Job(
        IngestionJobStatus status,
        DateTimeOffset activityUtc,
        bool fullBackfill) => new()
    {
        Id = Guid.CreateVersion7(),
        Type = IngestionJobType.OrderReconciliation,
        Status = status,
        QueuedUtc = activityUtc.AddMinutes(-1),
        StartedUtc = activityUtc.AddMinutes(-1),
        CompletedUtc = activityUtc,
        Checkpoint = $"{{\"phase\":\"complete\",\"isFullBackfill\":{fullBackfill.ToString().ToLowerInvariant()}}}"
    };

    private static void AssertCheck(
        ConversionReadinessReport report,
        string key,
        ConversionReadinessState state,
        bool blocksActivation)
    {
        var check = Assert.Single(report.Checks, item => item.Key == key);
        Assert.Equal(state, check.State);
        Assert.Equal(blocksActivation, check.BlocksActivation);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed record EvidenceCase(
        string Name,
        ConversionEvidenceAcknowledgement Acknowledgement,
        string ExpectedText);

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }
}
