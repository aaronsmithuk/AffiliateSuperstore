using AffiliateSuperstore.Application.Catalogue;
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
}
