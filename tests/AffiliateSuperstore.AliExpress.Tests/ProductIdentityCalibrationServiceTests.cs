using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class ProductIdentityCalibrationServiceTests
{
    [Fact]
    public async Task AddLabelAsync_RecordsAuditLabelWithoutChangingCanonicalMembership()
    {
        var factory = await CreateDatabaseAsync();
        var service = new ProductIdentityCalibrationService(factory, TimeProvider.System);
        await using var lookup = factory.CreateDbContext();
        var candidateId = await lookup.ProductMatchCandidates.Select(item => item.Id).FirstAsync();

        var added = await service.AddLabelAsync(
            candidateId,
            ProductRelationship.Duplicate,
            ProductIdentityGoldSetSlice.ThresholdSelection,
            "Same trusted identifier and matching product facts.",
            "reviewer-one");
        var duplicate = await service.AddLabelAsync(
            candidateId,
            ProductRelationship.Duplicate,
            ProductIdentityGoldSetSlice.ThresholdSelection,
            "Same trusted identifier and matching product facts.",
            "reviewer-one");
        var moved = await service.AddLabelAsync(
            candidateId,
            ProductRelationship.Duplicate,
            ProductIdentityGoldSetSlice.FinalTest,
            "Trying to move this pair into a different slice.",
            "reviewer-two");

        Assert.True(added.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.False(moved.Succeeded);
        await using var context = factory.CreateDbContext();
        Assert.Single(await context.ProductIdentityGoldLabels.ToListAsync());
        Assert.Empty(await context.CanonicalProductMembers.ToListAsync());
        Assert.All(await context.ProductMatchCandidates.ToListAsync(), item => Assert.Equal(ProductMatchReviewStatus.Pending, item.ReviewStatus));
    }

    [Fact]
    public async Task BuildReportAsync_ExcludesDisagreementUntilAdjudicatedAndCalculatesThresholds()
    {
        var factory = await CreateDatabaseAsync();
        await using (var seed = factory.CreateDbContext())
        {
            var candidates = await seed.ProductMatchCandidates.OrderBy(item => item.Confidence).ToArrayAsync();
            var now = DateTimeOffset.UtcNow;
            Add(seed, candidates[3], ProductRelationship.Duplicate, "r1", now);
            Add(seed, candidates[3], ProductRelationship.Duplicate, "r2", now.AddSeconds(1));
            Add(seed, candidates[2], ProductRelationship.NotRelated, "r1", now);
            Add(seed, candidates[2], ProductRelationship.Duplicate, "r2", now.AddSeconds(1));
            Add(seed, candidates[1], ProductRelationship.Variant, "r1", now);
            Add(seed, candidates[1], ProductRelationship.Duplicate, "r2", now.AddSeconds(1));
            Add(seed, candidates[1], ProductRelationship.Variant, "editor", now.AddSeconds(2), true);
            Add(seed, candidates[0], ProductRelationship.Bundle, "r1", now);
            await seed.SaveChangesAsync();
        }
        var service = new ProductIdentityCalibrationService(factory, TimeProvider.System);

        var report = await service.BuildReportAsync("plushies");

        Assert.Equal(4, report.CandidatePairs);
        Assert.Equal(8, report.IndividualLabels);
        Assert.Equal(3, report.EffectivePairs);
        Assert.Equal(1, report.SingleReviewedPairs);
        Assert.Equal(3, report.DoubleReviewedPairs);
        Assert.Equal(1, report.AgreementPairs);
        Assert.Equal(2, report.DisagreementPairs);
        Assert.Equal(1, report.AdjudicatedPairs);
        Assert.Equal(1m, report.CandidatePrecision);
        Assert.Equal(.66667m, report.RelationshipAccuracy);
        var automatic = report.Thresholds.Single(item => item.Threshold == .985m);
        Assert.Equal(2, automatic.AutoLinkEvaluatedPairs);
        Assert.Equal(1, automatic.AutoLinkCorrectPairs);
        Assert.Equal(1, automatic.FalseMergePairs);
        Assert.Equal(.5m, automatic.AutoLinkPrecision);
        Assert.True(automatic.AutoLinkWilsonLowerBound < .5m);
    }

    [Fact]
    public async Task AddLabelAsync_RequiresTwoConflictingReviewersBeforeAdjudication()
    {
        var factory = await CreateDatabaseAsync();
        var service = new ProductIdentityCalibrationService(factory, TimeProvider.System);
        await using var lookup = factory.CreateDbContext();
        var candidateId = await lookup.ProductMatchCandidates.Select(item => item.Id).FirstAsync();

        await service.AddLabelAsync(candidateId, ProductRelationship.Duplicate, ProductIdentityGoldSetSlice.Tuning,
            "The title and trusted identifier agree.", "reviewer-one");
        var early = await service.AddLabelAsync(candidateId, ProductRelationship.Variant, ProductIdentityGoldSetSlice.Tuning,
            "The size evidence shows a meaningful variant.", "editor", true);
        await service.AddLabelAsync(candidateId, ProductRelationship.Variant, ProductIdentityGoldSetSlice.Tuning,
            "The size evidence shows a meaningful variant.", "reviewer-two");
        var adjudicated = await service.AddLabelAsync(candidateId, ProductRelationship.Variant, ProductIdentityGoldSetSlice.Tuning,
            "The explicit size conflict outweighs title similarity.", "editor", true);

        Assert.False(early.Succeeded);
        Assert.True(adjudicated.Succeeded);
    }

    private static void Add(
        AffiliateSuperstoreDbContext context,
        ProductMatchCandidateRecord candidate,
        ProductRelationship label,
        string reviewer,
        DateTimeOffset createdUtc,
        bool adjudication = false) => context.ProductIdentityGoldLabels.Add(new ProductIdentityGoldLabelRecord
    {
        Id = Guid.CreateVersion7(),
        CandidateId = candidate.Id,
        Label = label,
        Slice = ProductIdentityGoldSetSlice.ThresholdSelection,
        Reviewer = reviewer,
        Rationale = "Reviewer evidence rationale for deterministic evaluation.",
        IsAdjudication = adjudication,
        CreatedUtc = createdUtc
    });

    private static async Task<InMemoryFactory> CreateDatabaseAsync()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await using var context = factory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
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
            CreatedUtc = now,
            UpdatedUtc = now
        });
        for (var index = 0; index < 8; index++)
        {
            context.Products.Add(Product($"p{index}", $"Product {index}", now));
            context.ShopProducts.Add(new ShopProductRecord
            {
                ShopId = shopId,
                ProductId = $"p{index}",
                IsActive = true,
                FirstIncludedUtc = now,
                LastIncludedUtc = now
            });
        }
        context.ProductMatchCandidates.AddRange(
            Candidate("p0", "p1", ProductRelationship.Bundle, .92m, "[\"pack count differs\"]", now),
            Candidate("p2", "p3", ProductRelationship.Duplicate, .985m, null, now),
            Candidate("p4", "p5", ProductRelationship.Duplicate, .99m, null, now),
            Candidate("p6", "p7", ProductRelationship.Duplicate, .995m, null, now));
        await context.SaveChangesAsync();
        return factory;
    }

    private static ProductRecord Product(string id, string title, DateTimeOffset now) => new()
    {
        AliExpressProductId = id,
        Title = title,
        IsEligible = true,
        FirstSeenUtc = now,
        LastSeenUtc = now,
        LastRefreshedUtc = now,
        LastCheckedUtc = now
    };

    private static ProductMatchCandidateRecord Candidate(
        string left,
        string right,
        ProductRelationship relationship,
        decimal confidence,
        string? conflicts,
        DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        LeftProductId = left,
        RightProductId = right,
        SuggestedRelationship = relationship,
        Confidence = confidence,
        BlockingReason = "deterministic evidence",
        EvidenceJson = "{}",
        ConflictJson = conflicts,
        MatcherVersion = ProductIdentityService.MatcherVersion,
        GeneratedUtc = now
    };

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }
}
