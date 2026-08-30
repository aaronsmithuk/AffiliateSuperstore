using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Core.Shops;
using AffiliateSuperstore.Persistence.Entities;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class CatalogueAutomationPlannerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 20, 0, 0, TimeSpan.Zero);
    private static readonly CatalogueAutomationOptions Options = new()
    {
        RefreshEveryHours = 24,
        FailureRetryMinutes = 60,
        StaleJobHours = 2
    };

    [Fact]
    public void IsDue_WhenShopHasNeverRun_ReturnsTrue()
    {
        Assert.True(CatalogueAutomationPlanner.IsDue(null, null, null, Now, Options));
    }

    [Theory]
    [InlineData(-23, false)]
    [InlineData(-24, true)]
    [InlineData(-48, true)]
    public void IsDue_ForSuccessfulJob_UsesNormalRefreshInterval(int completedHours, bool expected)
    {
        var result = CatalogueAutomationPlanner.IsDue(
            IngestionJobStatus.Succeeded,
            Now.AddHours(completedHours - 1),
            Now.AddHours(completedHours),
            Now,
            Options);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-59, false)]
    [InlineData(-60, true)]
    public void IsDue_ForFailedJob_UsesShorterRetryInterval(int completedMinutes, bool expected)
    {
        var result = CatalogueAutomationPlanner.IsDue(
            IngestionJobStatus.Failed,
            Now.AddMinutes(completedMinutes - 1),
            Now.AddMinutes(completedMinutes),
            Now,
            Options);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(-2, true)]
    public void IsDue_ForRunningJob_OnlyRecoversAfterStaleThreshold(int startedHours, bool expected)
    {
        var result = CatalogueAutomationPlanner.IsDue(
            IngestionJobStatus.Running,
            Now.AddHours(startedHours),
            null,
            Now,
            Options);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void DiscoveryPlan_UsesDefaultQueryWhenNoExpandedQueriesAreConfigured()
    {
        var shop = new ShopDefinition
        {
            Slug = "plushies",
            DefaultSearchQuery = "plush toy",
            DiscoveryPagesPerQuery = 1
        };

        var plan = CatalogueDiscoveryPlanner.Build(shop, 20);

        var request = Assert.Single(plan);
        Assert.Equal("plush toy", request.Keywords);
        Assert.Equal(1, request.PageNumber);
        Assert.Equal(20, request.PageSize);
    }

    [Fact]
    public void DiscoveryPlan_DeduplicatesQueriesAndExpandsPagesDeterministically()
    {
        var shop = new ShopDefinition
        {
            Slug = "plushies",
            DefaultSearchQuery = "plush toy",
            DiscoveryQueries = [" plush toy ", "PLUSH TOY", "stuffed animal"],
            DiscoveryPagesPerQuery = 2
        };

        var plan = CatalogueDiscoveryPlanner.Build(shop, 30);

        Assert.Collection(
            plan,
            request => Assert.Equal(("plush toy", 1), (request.Keywords, request.PageNumber)),
            request => Assert.Equal(("plush toy", 2), (request.Keywords, request.PageNumber)),
            request => Assert.Equal(("stuffed animal", 1), (request.Keywords, request.PageNumber)),
            request => Assert.Equal(("stuffed animal", 2), (request.Keywords, request.PageNumber)));
    }
}
