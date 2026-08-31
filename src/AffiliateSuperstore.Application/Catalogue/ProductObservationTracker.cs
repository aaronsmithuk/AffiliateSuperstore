using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AffiliateSuperstore.AliExpress;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;

namespace AffiliateSuperstore.Application.Catalogue;

internal sealed record ProductObservationOutcome(
    bool SnapshotCreated,
    bool ContentChanged,
    bool AvailabilityStateChanged,
    bool Restored,
    bool SuspectedUnavailable,
    bool ConfirmedUnavailable);

internal static class ProductObservationTracker
{
    public static readonly TimeSpan MinimumUnavailableConfirmationWindow = TimeSpan.FromHours(24);
    public const string AvailabilityIneligibilityPrefix = "availability:";
    private const string ParserVersion = "1.0";

    public static ProductObservationOutcome RecordReturned(
        AffiliateSuperstoreDbContext context,
        ProductRecord product,
        AliExpressProduct source,
        string sourceEndpoint,
        string correlationId,
        DateTimeOffset observedUtc)
    {
        var rawJson = JsonSerializer.Serialize(source);
        var observationHash = Hash(rawJson);
        var contentHash = Hash(JsonSerializer.Serialize(CreateContentFingerprint(source)));
        var previousContentHash = product.CurrentContentHash;
        var contentChanged = !string.Equals(previousContentHash, contentHash, StringComparison.Ordinal);
        var snapshotCreated = false;

        if (contentChanged)
        {
            context.ProductSnapshots.Add(CreateSnapshot(
                source,
                sourceEndpoint,
                correlationId,
                observedUtc,
                observationHash,
                contentHash,
                rawJson));
            context.ProductChangeEvents.Add(new ProductChangeEventRecord
            {
                Id = Guid.CreateVersion7(),
                ProductId = product.AliExpressProductId,
                Kind = previousContentHash is null
                    ? ProductChangeEventKind.ObservationCreated
                    : ProductChangeEventKind.ContentChanged,
                OccurredUtc = observedUtc,
                EvidenceSource = sourceEndpoint,
                CorrelationId = correlationId,
                PreviousValue = previousContentHash,
                CurrentValue = contentHash,
                ObservationHash = observationHash
            });
            snapshotCreated = true;
        }

        var previousState = product.AvailabilityState;
        var restored = previousState != ProductAvailabilityState.Available;
        product.AvailabilityState = ProductAvailabilityState.Available;
        product.AvailabilityReason = null;
        product.AvailabilityChangedUtc = restored ? observedUtc : product.AvailabilityChangedUtc;
        product.ConsecutiveUnavailableChecks = 0;
        product.FirstUnavailableEvidenceUtc = null;
        product.LastUnavailableEvidenceUtc = null;
        product.LastCheckedUtc = observedUtc;
        product.LastSuccessfulCheckUtc = observedUtc;
        product.LastSeenUtc = observedUtc;
        product.CurrentObservationHash = observationHash;
        product.CurrentContentHash = contentHash;

        if (!product.IsEligible &&
            product.IneligibilityReason?.StartsWith(AvailabilityIneligibilityPrefix, StringComparison.Ordinal) == true)
        {
            product.IsEligible = true;
            product.IneligibilityReason = null;
        }

        if (restored)
        {
            AddAvailabilityChange(
                context,
                product,
                previousState,
                ProductAvailabilityState.Available,
                sourceEndpoint,
                correlationId,
                observedUtc,
                observationHash,
                "The source returned the product again.");
        }

        return new(
            snapshotCreated,
            contentChanged,
            restored,
            restored,
            false,
            false);
    }

    public static ProductObservationOutcome RecordMissingDirect(
        AffiliateSuperstoreDbContext context,
        ProductRecord product,
        string sourceEndpoint,
        string correlationId,
        DateTimeOffset checkedUtc)
    {
        product.LastCheckedUtc = checkedUtc;
        if (product.LastUnavailableEvidenceUtc is not null &&
            checkedUtc <= product.LastUnavailableEvidenceUtc.Value)
        {
            return new(false, false, false, false,
                product.AvailabilityState == ProductAvailabilityState.SuspectedUnavailable,
                product.AvailabilityState == ProductAvailabilityState.Unavailable);
        }

        var previousState = product.AvailabilityState;
        product.FirstUnavailableEvidenceUtc ??= checkedUtc;
        product.LastUnavailableEvidenceUtc = checkedUtc;
        product.ConsecutiveUnavailableChecks++;

        var confirmationWindowElapsed =
            checkedUtc - product.FirstUnavailableEvidenceUtc.Value >= MinimumUnavailableConfirmationWindow;
        var nextState = product.ConsecutiveUnavailableChecks >= 2 && confirmationWindowElapsed
            ? ProductAvailabilityState.Unavailable
            : ProductAvailabilityState.SuspectedUnavailable;
        product.AvailabilityState = nextState;
        product.AvailabilityReason = nextState == ProductAvailabilityState.Unavailable
            ? $"The product was absent from {product.ConsecutiveUnavailableChecks} direct source checks spanning at least 24 hours."
            : "The product was absent from a direct source check and remains visible pending confirmation.";

        context.ProductChangeEvents.Add(new ProductChangeEventRecord
        {
            Id = Guid.CreateVersion7(),
            ProductId = product.AliExpressProductId,
            Kind = ProductChangeEventKind.UnavailableEvidence,
            OccurredUtc = checkedUtc,
            EvidenceSource = sourceEndpoint,
            CorrelationId = correlationId,
            PreviousValue = Math.Max(0, product.ConsecutiveUnavailableChecks - 1).ToString(CultureInfo.InvariantCulture),
            CurrentValue = product.ConsecutiveUnavailableChecks.ToString(CultureInfo.InvariantCulture),
            ObservationHash = product.CurrentObservationHash,
            DetailsJson = JsonSerializer.Serialize(new
            {
                product.FirstUnavailableEvidenceUtc,
                product.LastUnavailableEvidenceUtc,
                ConfirmationWindowElapsed = confirmationWindowElapsed
            })
        });

        var stateChanged = previousState != nextState;
        if (stateChanged)
        {
            product.AvailabilityChangedUtc = checkedUtc;
            AddAvailabilityChange(
                context,
                product,
                previousState,
                nextState,
                sourceEndpoint,
                correlationId,
                checkedUtc,
                product.CurrentObservationHash,
                product.AvailabilityReason);
        }

        if (nextState == ProductAvailabilityState.Unavailable &&
            (product.IsEligible || string.IsNullOrWhiteSpace(product.IneligibilityReason) ||
             product.IneligibilityReason.StartsWith(AvailabilityIneligibilityPrefix, StringComparison.Ordinal)))
        {
            product.IsEligible = false;
            product.IneligibilityReason = AvailabilityIneligibilityPrefix + "confirmed-unavailable";
        }

        return new(
            false,
            false,
            stateChanged,
            false,
            nextState == ProductAvailabilityState.SuspectedUnavailable,
            stateChanged && nextState == ProductAvailabilityState.Unavailable);
    }

    private static void AddAvailabilityChange(
        AffiliateSuperstoreDbContext context,
        ProductRecord product,
        ProductAvailabilityState previousState,
        ProductAvailabilityState currentState,
        string sourceEndpoint,
        string correlationId,
        DateTimeOffset occurredUtc,
        string? observationHash,
        string? reason)
    {
        context.ProductChangeEvents.Add(new ProductChangeEventRecord
        {
            Id = Guid.CreateVersion7(),
            ProductId = product.AliExpressProductId,
            Kind = ProductChangeEventKind.AvailabilityChanged,
            OccurredUtc = occurredUtc,
            EvidenceSource = sourceEndpoint,
            CorrelationId = correlationId,
            PreviousValue = previousState.ToString(),
            CurrentValue = currentState.ToString(),
            ObservationHash = observationHash,
            DetailsJson = string.IsNullOrWhiteSpace(reason) ? null : JsonSerializer.Serialize(new { Reason = reason })
        });
    }

    private static ProductSnapshotRecord CreateSnapshot(
        AliExpressProduct product,
        string sourceEndpoint,
        string correlationId,
        DateTimeOffset observedUtc,
        string observationHash,
        string contentHash,
        string rawJson) => new()
    {
        ProductId = product.ProductId,
        FetchedUtc = observedUtc,
        SalePrice = ParseDecimal(product.TargetSalePrice),
        OriginalPrice = ParseDecimal(product.TargetOriginalPrice),
        Currency = product.Currency ?? "GBP",
        CommissionRate = ParseRate(product.CommissionRate),
        HotProductCommissionRate = ParseRate(product.HotProductCommissionRate),
        DiscountText = product.Discount,
        EvaluationRate = ParseRate(product.EvaluationRate),
        RecentSalesVolume = product.RecentSalesVolume,
        TaxRate = ParseRate(product.TaxRate),
        IsAvailable = true,
        ObservationHash = observationHash,
        ContentHash = contentHash,
        SourceEndpoint = sourceEndpoint,
        CorrelationId = correlationId,
        ParserVersion = ParserVersion,
        RawJson = rawJson
    };

    private static object CreateContentFingerprint(AliExpressProduct product) => new
    {
        ProductId = Normalize(product.ProductId),
        Title = Normalize(product.Title),
        MainImageUrl = Normalize(product.MainImageUrl),
        ProductDetailUrl = Normalize(product.ProductDetailUrl),
        SmallImageUrls = (product.SmallImageUrls ?? []).Select(Normalize).ToArray(),
        VideoUrl = Normalize(product.VideoUrl),
        SalePrice = NormalizeNumber(product.TargetSalePrice),
        OriginalPrice = NormalizeNumber(product.TargetOriginalPrice),
        Currency = Normalize(product.Currency).ToUpperInvariant(),
        CommissionRate = NormalizeNumber(product.CommissionRate),
        HotProductCommissionRate = NormalizeNumber(product.HotProductCommissionRate),
        Discount = Normalize(product.Discount),
        EvaluationRate = NormalizeNumber(product.EvaluationRate),
        product.RecentSalesVolume,
        FirstLevelCategoryId = Normalize(product.FirstLevelCategoryId),
        FirstLevelCategoryName = Normalize(product.FirstLevelCategoryName),
        SecondLevelCategoryId = Normalize(product.SecondLevelCategoryId),
        SecondLevelCategoryName = Normalize(product.SecondLevelCategoryName),
        SellerId = Normalize(product.ShopId),
        SellerName = Normalize(product.ShopName),
        SellerUrl = Normalize(product.ShopUrl),
        TaxRate = NormalizeNumber(product.TaxRate),
        SkuId = Normalize(product.SkuId),
        EanCode = Normalize(product.EanCode)
    };

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;

    private static string NormalizeNumber(string? value)
    {
        var parsed = ParseDecimal(value);
        return parsed?.ToString("0.############################", CultureInfo.InvariantCulture) ?? Normalize(value);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value?.Trim().TrimEnd('%'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static decimal? ParseRate(string? value)
    {
        var parsed = ParseDecimal(value);
        if (parsed is null) return null;
        return value?.Contains('%', StringComparison.Ordinal) == true || parsed > 1 ? parsed / 100 : parsed;
    }
}
