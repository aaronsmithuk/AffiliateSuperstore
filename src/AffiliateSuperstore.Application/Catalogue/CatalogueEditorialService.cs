using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.Application.Catalogue;

public sealed record CatalogueEditorialUpdate(
    string ShopSlug,
    string ProductId,
    string? EditorialTitle,
    string? EditorialDescription,
    bool IsFeatured,
    int DisplayOrder,
    string? ExpectedRowVersion = null,
    string EditedBy = "administrator",
    string? ChangeReason = null,
    string? VerifiedSize = null,
    string? VerifiedOptions = null,
    string? VerificationEvidence = null);

public sealed record CatalogueCommandResult(
    bool Succeeded,
    string Message,
    Guid? VersionId = null,
    IReadOnlyList<EditorialValidationFinding>? Findings = null);

public sealed record EditorialVersionHistoryEntry(
    Guid Id,
    int VersionNumber,
    string? EditorialTitle,
    string? EditorialDescription,
    string? VerifiedSize,
    string? VerifiedOptions,
    string? VerificationEvidence,
    bool IsFeatured,
    int DisplayOrder,
    EditorialVersionChangeKind ChangeKind,
    int? RolledBackFromVersionNumber,
    string? ChangeReason,
    string CreatedBy,
    DateTimeOffset CreatedUtc,
    EditorialValidationState ValidationState,
    IReadOnlyList<EditorialValidationFinding> Findings,
    IReadOnlyList<string> ChangedFields,
    string ContentHash);

public sealed class CatalogueEditorialService(
    IDbContextFactory<AffiliateSuperstoreDbContext> contextFactory,
    ProductQualityAssessmentService qualityAssessmentService,
    EditorialContentValidator editorialValidator,
    TimeProvider timeProvider)
{
    public const int MaximumTitleLength = 180;
    public const int MaximumDescriptionLength = 1000;
    public const int MaximumVerifiedSizeLength = 300;
    public const int MaximumVerifiedOptionsLength = 600;
    public const int MaximumVerificationEvidenceLength = 1000;
    public const int MaximumDisplayOrder = 10_000;
    public const int MaximumChangeReasonLength = 500;
    public const int MaximumActorLength = 256;

    public async Task<CatalogueCommandResult> SaveAsync(
        CatalogueEditorialUpdate update,
        CancellationToken cancellationToken = default)
    {
        var title = Normalise(update.EditorialTitle);
        var description = Normalise(update.EditorialDescription);
        var verifiedSize = Normalise(update.VerifiedSize);
        var verifiedOptions = Normalise(update.VerifiedOptions);
        var verificationEvidence = Normalise(update.VerificationEvidence);
        var inputError = ValidateInput(title, description, verifiedSize, verifiedOptions, verificationEvidence, update.DisplayOrder);
        if (inputError is not null) return Failure(inputError);
        if (Normalise(update.ChangeReason)?.Length > MaximumChangeReasonLength) return Failure($"The change note must be {MaximumChangeReasonLength} characters or fewer.");
        if (NormaliseActor(update.EditedBy).Length > MaximumActorLength) return Failure("The editor identity is too long to store safely.");

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await FindProductAsync(context, update.ShopSlug, update.ProductId, cancellationToken);
        if (item is null) return Failure("The catalogue product could not be found.");
        if (!MatchesRowVersion(item.RowVersion, update.ExpectedRowVersion))
        {
            return Failure("This product changed after you opened it. Reload the editor before saving so another administrator's work is not overwritten.");
        }

        var validation = ValidateEditorial(item, title, description);
        if (validation.IsBlocked)
        {
            return Failure("Editorial changes were not saved because one or more claims lack source evidence.", validation.Findings);
        }

        var latest = await LatestVersionAsync(context, item, cancellationToken);
        if (latest is null && HasExistingProjection(item))
        {
            latest = CreateVersion(item, 1, EditorialVersionChangeKind.Imported, "system migration", "Imported the pre-versioning catalogue projection.", null,
                ValidateEditorial(item, item.EditorialTitle, item.EditorialDescription), item.EditorialTitle, item.EditorialDescription,
                item.VerifiedSize, item.VerifiedOptions, item.VerificationEvidence, item.IsFeatured, item.DisplayOrder);
            context.EditorialVersions.Add(latest);
            ApplyVersionProjection(item, latest);
        }

        var proposedHash = ContentHash(title, description, verifiedSize, verifiedOptions, verificationEvidence, update.IsFeatured, update.DisplayOrder);
        if (latest is not null && latest.ContentHash == proposedHash && latest.ValidatorVersion == EditorialContentValidator.Version)
        {
            ApplyValidationProjection(item, validation);
            ApplyQualityAssessment(item);
            var demoted = false;
            if (item.ReviewStatus == ProductReviewStatus.Approved &&
                (validation.State != EditorialValidationState.Passed || HasQualityFlags(item)))
            {
                HoldForReview(item);
                demoted = true;
            }
            await context.SaveChangesAsync(cancellationToken);
            return Success(demoted
                ? "No editorial content changed, but the product was returned to review because it no longer passes the current publication checks."
                : "No editorial changes were detected.", latest.Id, validation.Findings);
        }

        var version = CreateVersion(
            item,
            (latest?.VersionNumber ?? 0) + 1,
            EditorialVersionChangeKind.Edit,
            NormaliseActor(update.EditedBy),
            Normalise(update.ChangeReason),
            null,
            validation,
            title,
            description,
            verifiedSize,
            verifiedOptions,
            verificationEvidence,
            update.IsFeatured,
            update.DisplayOrder);
        context.EditorialVersions.Add(version);
        ApplyVersionProjection(item, version);
        ApplyQualityAssessment(item);
        if (item.ReviewStatus == ProductReviewStatus.Approved)
        {
            item.ReviewStatus = ProductReviewStatus.NeedsReview;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Success(validation.State == EditorialValidationState.Warning
            ? "Editorial revision saved with warnings. Resolve them before approval."
            : "Editorial revision saved and is ready for review.", version.Id, validation.Findings);
    }

    public async Task<IReadOnlyList<EditorialVersionHistoryEntry>> GetHistoryAsync(
        string shopSlug,
        string productId,
        int take = 30,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var versions = await context.EditorialVersions
            .AsNoTracking()
            .Where(item => item.ShopProduct.Shop.Slug == shopSlug && item.ProductId == productId)
            .OrderBy(item => item.VersionNumber)
            .ToListAsync(cancellationToken);
        if (versions.Count == 0) return [];

        var versionNumbers = versions.ToDictionary(item => item.Id, item => item.VersionNumber);
        var entries = new List<EditorialVersionHistoryEntry>(versions.Count);
        EditorialVersionRecord? previous = null;
        foreach (var version in versions)
        {
            entries.Add(new EditorialVersionHistoryEntry(
                version.Id,
                version.VersionNumber,
                version.EditorialTitle,
                version.EditorialDescription,
                version.VerifiedSize,
                version.VerifiedOptions,
                version.VerificationEvidence,
                version.IsFeatured,
                version.DisplayOrder,
                version.ChangeKind,
                version.RolledBackFromVersionId is Guid targetId && versionNumbers.TryGetValue(targetId, out var targetNumber) ? targetNumber : null,
                version.ChangeReason,
                version.CreatedBy,
                version.CreatedUtc,
                version.ValidationState,
                EditorialContentValidator.ReadFindings(version.ValidationFindingsJson),
                ChangedFields(previous, version),
                version.ContentHash));
            previous = version;
        }

        return entries.OrderByDescending(item => item.VersionNumber).Take(Math.Clamp(take, 1, 100)).ToArray();
    }

    public async Task<CatalogueCommandResult> RollbackAsync(
        string shopSlug,
        string productId,
        Guid targetVersionId,
        string editedBy,
        string? reason = null,
        string? expectedRowVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (Normalise(reason)?.Length > MaximumChangeReasonLength) return Failure($"The change note must be {MaximumChangeReasonLength} characters or fewer.");
        if (NormaliseActor(editedBy).Length > MaximumActorLength) return Failure("The editor identity is too long to store safely.");
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await FindProductAsync(context, shopSlug, productId, cancellationToken);
        if (item is null) return Failure("The catalogue product could not be found.");
        if (!MatchesRowVersion(item.RowVersion, expectedRowVersion))
        {
            return Failure("This product changed after you opened it. Reload the editor before restoring a revision.");
        }

        var target = await context.EditorialVersions.SingleOrDefaultAsync(
            version => version.Id == targetVersionId && version.ShopId == item.ShopId && version.ProductId == item.ProductId,
            cancellationToken);
        if (target is null) return Failure("The requested editorial revision could not be found.");

        var validation = ValidateEditorial(item, target.EditorialTitle, target.EditorialDescription);
        if (validation.IsBlocked)
        {
            return Failure("That revision cannot be restored because its claims no longer pass the current source-evidence rules.", validation.Findings);
        }

        var latest = await LatestVersionAsync(context, item, cancellationToken);
        var version = CreateVersion(
            item,
            (latest?.VersionNumber ?? 0) + 1,
            EditorialVersionChangeKind.Rollback,
            NormaliseActor(editedBy),
            Normalise(reason) ?? $"Restored revision {target.VersionNumber}.",
            target.Id,
            validation,
            target.EditorialTitle,
            target.EditorialDescription,
            target.VerifiedSize,
            target.VerifiedOptions,
            target.VerificationEvidence,
            target.IsFeatured,
            target.DisplayOrder);
        context.EditorialVersions.Add(version);
        ApplyVersionProjection(item, version);
        ApplyQualityAssessment(item);
        HoldForReview(item);
        await context.SaveChangesAsync(cancellationToken);
        return Success($"Revision {target.VersionNumber} was restored as new revision {version.VersionNumber} and returned to review.", version.Id, validation.Findings);
    }

    public async Task<CatalogueCommandResult> SetReviewStatusAsync(
        string shopSlug,
        string productId,
        ProductReviewStatus status,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await FindProductAsync(context, shopSlug, productId, cancellationToken);
        if (item is null) return Failure("The catalogue product could not be found.");

        if (status == ProductReviewStatus.Approved)
        {
            ApplyQualityAssessment(item);
            var validation = ValidateEditorial(item, item.EditorialTitle, item.EditorialDescription);
            ApplyValidationProjection(item, validation);
            await EnsureBaselineVersionAsync(context, item, validation, cancellationToken);
            if (!item.IsActive || !item.Product.IsEligible)
            {
                HoldForReview(item);
                await context.SaveChangesAsync(cancellationToken);
                return Failure("This product is inactive or no longer eligible and cannot be published.", validation.Findings);
            }
            if (HasQualityFlags(item))
            {
                HoldForReview(item);
                await context.SaveChangesAsync(cancellationToken);
                return Failure("Resolve or reject the product's quality flags before approving it.", validation.Findings);
            }
            if (validation.State != EditorialValidationState.Passed)
            {
                HoldForReview(item);
                await context.SaveChangesAsync(cancellationToken);
                return Failure(validation.IsBlocked
                    ? "Editorial claims lack source evidence and must be corrected before approval."
                    : "Resolve the editorial warnings before approving this product.", validation.Findings);
            }

            var hasActiveLink = await context.AffiliateLinks.AnyAsync(
                link => link.ShopId == item.ShopId && link.ProductId == item.ProductId && link.Status == AffiliateLinkStatus.Active,
                cancellationToken);
            if (!hasActiveLink)
            {
                HoldForReview(item);
                await context.SaveChangesAsync(cancellationToken);
                return Failure("Generate a working affiliate link before approving this product.", validation.Findings);
            }
        }

        item.ReviewStatus = status;
        item.DisabledReason = status == ProductReviewStatus.Rejected ? "Rejected during catalogue review." : null;
        await context.SaveChangesAsync(cancellationToken);
        return Success(status switch
        {
            ProductReviewStatus.Approved => "Product approved and visible in the public shop.",
            ProductReviewStatus.Rejected => "Product rejected and removed from the public shop.",
            ProductReviewStatus.NeedsReview => "Product returned to the manual review queue.",
            _ => "Product review status updated."
        });
    }

    private EditorialValidationResult ValidateEditorial(ShopProductRecord item, string? title, string? description) =>
        editorialValidator.Validate(new EditorialValidationInput(
            item.Product.Title,
            title,
            description,
            item.Product.IdentityProfile?.Material));

    private void ApplyQualityAssessment(ShopProductRecord item)
    {
        var assessment = qualityAssessmentService.AssessForPublication(item.Product.Title, item.EditorialTitle,
            item.Product.FirstLevelCategoryName, item.Product.SecondLevelCategoryName);
        item.AutomatedReviewFlags = assessment.SerializedFlags;
        item.AutomatedReviewedUtc = timeProvider.GetUtcNow();
    }

    private async Task EnsureBaselineVersionAsync(
        AffiliateSuperstoreDbContext context,
        ShopProductRecord item,
        EditorialValidationResult validation,
        CancellationToken cancellationToken)
    {
        if (await LatestVersionAsync(context, item, cancellationToken) is not null) return;
        var baseline = CreateVersion(item, 1, EditorialVersionChangeKind.Imported, "system migration",
            "Imported the pre-versioning catalogue projection.", null, validation,
            item.EditorialTitle, item.EditorialDescription, item.VerifiedSize, item.VerifiedOptions,
            item.VerificationEvidence, item.IsFeatured, item.DisplayOrder);
        context.EditorialVersions.Add(baseline);
        ApplyVersionProjection(item, baseline);
    }

    private EditorialVersionRecord CreateVersion(
        ShopProductRecord item,
        int versionNumber,
        EditorialVersionChangeKind changeKind,
        string createdBy,
        string? changeReason,
        Guid? rolledBackFromVersionId,
        EditorialValidationResult validation,
        string? title,
        string? description,
        string? verifiedSize,
        string? verifiedOptions,
        string? verificationEvidence,
        bool isFeatured,
        int displayOrder)
    {
        return new EditorialVersionRecord
        {
            Id = Guid.NewGuid(), ShopId = item.ShopId, ProductId = item.ProductId, VersionNumber = versionNumber,
            EditorialTitle = title, EditorialDescription = description, VerifiedSize = verifiedSize,
            VerifiedOptions = verifiedOptions, VerificationEvidence = verificationEvidence,
            IsFeatured = isFeatured, DisplayOrder = displayOrder,
            ChangeKind = changeKind, RolledBackFromVersionId = rolledBackFromVersionId, ChangeReason = changeReason,
            CreatedBy = createdBy, CreatedUtc = timeProvider.GetUtcNow(), ValidationState = validation.State,
            ValidationFindingsJson = validation.SerializedFindings, ValidatorVersion = EditorialContentValidator.Version,
            ContentHash = ContentHash(title, description, verifiedSize, verifiedOptions, verificationEvidence, isFeatured, displayOrder)
        };
    }

    private static void ApplyVersionProjection(ShopProductRecord item, EditorialVersionRecord version)
    {
        item.EditorialTitle = version.EditorialTitle;
        item.EditorialDescription = version.EditorialDescription;
        item.VerifiedSize = version.VerifiedSize;
        item.VerifiedOptions = version.VerifiedOptions;
        item.VerificationEvidence = version.VerificationEvidence;
        item.IsFeatured = version.IsFeatured;
        item.DisplayOrder = version.DisplayOrder;
        item.CurrentEditorialVersionNumber = version.VersionNumber;
        item.EditorialValidationState = version.ValidationState;
        item.EditorialValidationFlags = version.ValidationFindingsJson;
        item.EditorialValidatedUtc = version.CreatedUtc;
    }

    private void ApplyValidationProjection(ShopProductRecord item, EditorialValidationResult validation)
    {
        item.EditorialValidationState = validation.State;
        item.EditorialValidationFlags = validation.SerializedFindings;
        item.EditorialValidatedUtc = timeProvider.GetUtcNow();
    }

    private static Task<EditorialVersionRecord?> LatestVersionAsync(
        AffiliateSuperstoreDbContext context,
        ShopProductRecord item,
        CancellationToken cancellationToken) => context.EditorialVersions
            .Where(version => version.ShopId == item.ShopId && version.ProductId == item.ProductId)
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

    private static Task<ShopProductRecord?> FindProductAsync(
        AffiliateSuperstoreDbContext context,
        string shopSlug,
        string productId,
        CancellationToken cancellationToken) => context.ShopProducts
            .Include(item => item.Shop)
            .Include(item => item.Product).ThenInclude(product => product.IdentityProfile)
            .SingleOrDefaultAsync(item => item.Shop.Slug == shopSlug && item.ProductId == productId, cancellationToken);

    private static IReadOnlyList<string> ChangedFields(EditorialVersionRecord? previous, EditorialVersionRecord current)
    {
        if (previous is null) return ["Initial revision"];
        var changed = new List<string>();
        if (previous.EditorialTitle != current.EditorialTitle) changed.Add("Title");
        if (previous.EditorialDescription != current.EditorialDescription) changed.Add("Description");
        if (previous.VerifiedSize != current.VerifiedSize) changed.Add("Verified size");
        if (previous.VerifiedOptions != current.VerifiedOptions) changed.Add("Verified options");
        if (previous.VerificationEvidence != current.VerificationEvidence) changed.Add("Verification evidence");
        if (previous.IsFeatured != current.IsFeatured) changed.Add("Featured state");
        if (previous.DisplayOrder != current.DisplayOrder) changed.Add("Display order");
        return changed.Count == 0 ? ["Validation/provenance only"] : changed;
    }

    private static string ContentHash(
        string? title,
        string? description,
        string? verifiedSize,
        string? verifiedOptions,
        string? verificationEvidence,
        bool isFeatured,
        int displayOrder)
    {
        var json = JsonSerializer.Serialize(new
        {
            title,
            description,
            verifiedSize,
            verifiedOptions,
            verificationEvidence,
            isFeatured,
            displayOrder
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static bool MatchesRowVersion(byte[] actual, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected)) return true;
        try { return actual.SequenceEqual(Convert.FromBase64String(expected)); }
        catch (FormatException) { return false; }
    }

    private static string? ValidateInput(
        string? title,
        string? description,
        string? verifiedSize,
        string? verifiedOptions,
        string? verificationEvidence,
        int displayOrder)
    {
        if (title?.Length > MaximumTitleLength) return $"The editorial title must be {MaximumTitleLength} characters or fewer.";
        if (description?.Length > MaximumDescriptionLength) return $"The editorial description must be {MaximumDescriptionLength} characters or fewer.";
        if (verifiedSize?.Length > MaximumVerifiedSizeLength) return $"The verified size must be {MaximumVerifiedSizeLength} characters or fewer.";
        if (verifiedOptions?.Length > MaximumVerifiedOptionsLength) return $"The verified options must be {MaximumVerifiedOptionsLength} characters or fewer.";
        if (verificationEvidence?.Length > MaximumVerificationEvidenceLength) return $"The verification evidence must be {MaximumVerificationEvidenceLength} characters or fewer.";
        if ((verifiedSize is not null || verifiedOptions is not null) && verificationEvidence is null)
        {
            return "Add verification evidence before publishing a size or option statement.";
        }
        if (verifiedSize is null && verifiedOptions is null && verificationEvidence is not null)
        {
            return "Verification evidence needs a verified size or option statement.";
        }
        if (displayOrder is < 0 or > MaximumDisplayOrder) return $"Display order must be between 0 and {MaximumDisplayOrder:N0}.";
        return null;
    }

    private static bool HasExistingProjection(ShopProductRecord item) =>
        item.EditorialTitle is not null || item.EditorialDescription is not null ||
        item.VerifiedSize is not null || item.VerifiedOptions is not null || item.VerificationEvidence is not null ||
        item.IsFeatured || item.DisplayOrder != 0;
    private static bool HasQualityFlags(ShopProductRecord item) => ProductQualityAssessmentService.ReadFlags(item.AutomatedReviewFlags).Count > 0;
    private static void HoldForReview(ShopProductRecord item) { if (item.ReviewStatus != ProductReviewStatus.Rejected) item.ReviewStatus = ProductReviewStatus.NeedsReview; }
    private static string? Normalise(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string NormaliseActor(string? value) => Normalise(value) ?? "administrator";
    private static CatalogueCommandResult Success(string message, Guid? versionId = null, IReadOnlyList<EditorialValidationFinding>? findings = null) => new(true, message, versionId, findings);
    private static CatalogueCommandResult Failure(string message, IReadOnlyList<EditorialValidationFinding>? findings = null) => new(false, message, null, findings);
}
