using System.Buffers;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record ProductImageFingerprintResult(
    int ProductsSelected,
    int FingerprintsCreated,
    int FingerprintsUnchanged,
    int FailedOrSkipped);

public sealed class ProductImageFingerprintService(
    HttpClient httpClient,
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    TimeProvider timeProvider)
{
    public const string FingerprinterVersion = "1.0";
    public const int MaximumImageBytes = 5 * 1024 * 1024;
    public static readonly TimeSpan SuccessfulRefreshAge = TimeSpan.FromDays(30);
    public static readonly TimeSpan FailureRetryAge = TimeSpan.FromHours(24);
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);

    public async Task<ProductImageFingerprintResult> RefreshAsync(
        string shopSlug,
        int maximumProducts = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopSlug);
        if (maximumProducts is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(maximumProducts));
        await RefreshGate.WaitAsync(cancellationToken);
        try
        {
            return await RefreshCoreAsync(shopSlug.Trim(), maximumProducts, cancellationToken);
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    private async Task<ProductImageFingerprintResult> RefreshCoreAsync(
        string shopSlug,
        int maximumProducts,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var successBefore = now - SuccessfulRefreshAge;
        var failureBefore = now - FailureRetryAge;
        var products = await context.Products
            .AsNoTracking()
            .Include(item => item.ImageFingerprint)
            .Where(item => item.IsEligible && item.MainImageUrl != null &&
                item.Shops.Any(shopProduct => shopProduct.Shop.Slug == shopSlug && shopProduct.IsActive))
            .OrderByDescending(item => item.ImageFingerprint == null)
            .ThenBy(item => item.ImageFingerprint!.LastAttemptUtc)
            .Take(maximumProducts * 2)
            .ToArrayAsync(cancellationToken);
        var selected = products
            .Where(product => ShouldRefresh(product, successBefore, failureBefore))
            .Take(maximumProducts)
            .ToArray();
        var ids = selected.Select(product => product.AliExpressProductId).ToArray();
        var existing = await context.ProductImageFingerprints
            .Where(item => ids.Contains(item.ProductId))
            .ToDictionaryAsync(item => item.ProductId, cancellationToken);
        var created = 0;
        var unchanged = 0;
        var failed = 0;

        foreach (var product in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceUrl = product.MainImageUrl!.Trim();
            var sourceUrlHash = HashText(sourceUrl);
            var outcome = await FingerprintAsync(sourceUrl, cancellationToken);
            if (!existing.TryGetValue(product.AliExpressProductId, out var record))
            {
                record = new ProductImageFingerprintRecord { ProductId = product.AliExpressProductId };
                context.ProductImageFingerprints.Add(record);
                existing.Add(record.ProductId, record);
            }

            var sameContent = outcome.ContentSha256 is not null &&
                string.Equals(record.ContentSha256, outcome.ContentSha256, StringComparison.Ordinal);
            record.SourceUrl = sourceUrl;
            record.SourceUrlHash = sourceUrlHash;
            record.ContentSha256 = outcome.ContentSha256;
            record.ContentLength = outcome.ContentLength;
            record.ContentType = outcome.ContentType;
            record.Status = outcome.Status;
            record.FailureReason = outcome.FailureReason;
            record.AttemptCount++;
            record.LastAttemptUtc = now;
            record.FingerprintedUtc = outcome.Status == ProductImageFingerprintStatus.Succeeded ? now : record.FingerprintedUtc;
            record.FingerprinterVersion = FingerprinterVersion;
            if (outcome.Status == ProductImageFingerprintStatus.Succeeded)
            {
                if (sameContent) unchanged++; else created++;
            }
            else
            {
                failed++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return new(selected.Length, created, unchanged, failed);
    }

    private async Task<FingerprintOutcome> FingerprintAsync(string sourceUrl, CancellationToken cancellationToken)
    {
        if (!TryAllowedImageUri(sourceUrl, out var uri))
        {
            return FingerprintOutcome.Skipped("The image URL is outside the approved AliExpress CDN allow-list.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("WonderAisle-ImageFingerprint/1.0");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if ((int)response.StatusCode is < 200 or >= 300)
            {
                return FingerprintOutcome.Failed($"Image host returned HTTP {(int)response.StatusCode}.");
            }
            if (response.RequestMessage?.RequestUri is not { } finalUri || !IsAllowedCdnHost(finalUri))
            {
                return FingerprintOutcome.Skipped("The image request redirected outside the approved AliExpress CDN allow-list.");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is null || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return FingerprintOutcome.Skipped("The response was not an image.");
            }
            if (response.Content.Headers.ContentLength is > MaximumImageBytes)
            {
                return FingerprintOutcome.Skipped("The image exceeds the 5 MB fingerprint limit.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            long length = 0;
            try
            {
                int read;
                while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                {
                    length += read;
                    if (length > MaximumImageBytes) return FingerprintOutcome.Skipped("The image exceeds the 5 MB fingerprint limit.");
                    hash.AppendData(buffer, 0, read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
            if (length == 0) return FingerprintOutcome.Failed("The image response was empty.");
            return FingerprintOutcome.Succeeded(Convert.ToHexStringLower(hash.GetHashAndReset()), length, contentType);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return FingerprintOutcome.Failed("The image request timed out.");
        }
        catch (HttpRequestException exception)
        {
            return FingerprintOutcome.Failed($"Image download failed ({exception.HttpRequestError}).");
        }
    }

    private static bool ShouldRefresh(ProductRecord product, DateTimeOffset successBefore, DateTimeOffset failureBefore)
    {
        var fingerprint = product.ImageFingerprint;
        if (fingerprint is null) return true;
        if (!string.Equals(fingerprint.SourceUrlHash, HashText(product.MainImageUrl!), StringComparison.Ordinal) ||
            fingerprint.FingerprinterVersion != FingerprinterVersion) return true;
        return fingerprint.Status == ProductImageFingerprintStatus.Succeeded
            ? fingerprint.LastAttemptUtc <= successBefore
            : fingerprint.LastAttemptUtc <= failureBefore;
    }

    internal static bool TryAllowedImageUri(string value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var candidate) &&
            candidate.Scheme == Uri.UriSchemeHttps && IsAllowedCdnHost(candidate))
        {
            uri = candidate;
            return true;
        }
        uri = null!;
        return false;
    }

    private static bool IsAllowedCdnHost(Uri uri) =>
        HostMatches(uri.Host, "alicdn.com") || HostMatches(uri.Host, "aliexpress-media.com");

    private static bool HostMatches(string host, string suffix) =>
        host.Equals(suffix, StringComparison.OrdinalIgnoreCase) || host.EndsWith('.' + suffix, StringComparison.OrdinalIgnoreCase);

    private static string HashText(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record FingerprintOutcome(
        ProductImageFingerprintStatus Status,
        string? ContentSha256,
        long? ContentLength,
        string? ContentType,
        string? FailureReason)
    {
        public static FingerprintOutcome Succeeded(string hash, long length, string contentType) => new(ProductImageFingerprintStatus.Succeeded, hash, length, contentType, null);
        public static FingerprintOutcome Failed(string reason) => new(ProductImageFingerprintStatus.Failed, null, null, null, reason);
        public static FingerprintOutcome Skipped(string reason) => new(ProductImageFingerprintStatus.Skipped, null, null, null, reason);
    }
}
