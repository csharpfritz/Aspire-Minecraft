using Aspire.Hosting.Minecraft.Worker.Services;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Tests for ResourceStatusChange state tracking — shared logic across all Sprint 1 features.
/// Each feature should only fire RCON commands when status CHANGES, not on every poll.
/// </summary>
public class StateTransitionTrackingTests
{
    [Fact]
    public void ResourceStatusChange_RecordsPreviousAndNewStatus()
    {
        var change = new ResourceStatusChange("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy);

        Assert.Equal("api", change.Name);
        Assert.Equal("Project", change.Type);
        Assert.Equal(ResourceStatus.Healthy, change.OldStatus);
        Assert.Equal(ResourceStatus.Unhealthy, change.NewStatus);
    }

    [Fact]
    public void ResourceStatus_HasExpectedValues()
    {
        // Verify all expected states exist (features depend on these)
        Assert.Equal(0, (int)ResourceStatus.Unknown);
        Assert.Equal(1, (int)ResourceStatus.Healthy);
        Assert.Equal(2, (int)ResourceStatus.Unhealthy);
    }

    [Fact]
    public void ResourceStatusChange_SameStatusIsNotAChange()
    {
        // State transition tracking: Healthy → Healthy is not a change
        var change = new ResourceStatusChange("api", "Project", ResourceStatus.Healthy, ResourceStatus.Healthy);
        Assert.Equal(change.OldStatus, change.NewStatus);
    }

    [Fact]
    public void ResourceStatusChange_UnknownToHealthy_IsFirstDiscovery()
    {
        var change = new ResourceStatusChange("redis", "Container", ResourceStatus.Unknown, ResourceStatus.Healthy);

        Assert.Equal(ResourceStatus.Unknown, change.OldStatus);
        Assert.Equal(ResourceStatus.Healthy, change.NewStatus);
    }

    [Fact]
    public void ResourceStatusChange_HealthyToUnhealthy_IsDegradation()
    {
        var change = new ResourceStatusChange("db", "Postgres", ResourceStatus.Healthy, ResourceStatus.Unhealthy);

        Assert.Equal(ResourceStatus.Healthy, change.OldStatus);
        Assert.Equal(ResourceStatus.Unhealthy, change.NewStatus);
    }

    [Fact]
    public void ResourceStatusChange_UnhealthyToHealthy_IsRecovery()
    {
        var change = new ResourceStatusChange("api", "Project", ResourceStatus.Unhealthy, ResourceStatus.Healthy);

        Assert.Equal(ResourceStatus.Unhealthy, change.OldStatus);
        Assert.Equal(ResourceStatus.Healthy, change.NewStatus);
    }

    [Theory]
    [InlineData(0, 1)] // Unknown -> Healthy
    [InlineData(0, 2)] // Unknown -> Unhealthy
    [InlineData(1, 2)] // Healthy -> Unhealthy
    [InlineData(2, 1)] // Unhealthy -> Healthy
    public void ResourceStatusChange_AllValidTransitions_AreRecorded(int fromInt, int toInt)
    {
        var from = (ResourceStatus)fromInt;
        var to = (ResourceStatus)toInt;
        var change = new ResourceStatusChange("svc", "Project", from, to);

        Assert.NotEqual(change.OldStatus, change.NewStatus);
    }

    [Fact]
    public void ResourceInfo_WithStatus_CreatesNewRecordWithUpdatedStatus()
    {
        var info = new ResourceInfo("api", "Project", "http://localhost:5000", "", 0, ResourceStatus.Healthy);
        var updated = info with { Status = ResourceStatus.Unhealthy };

        Assert.Equal(ResourceStatus.Healthy, info.Status);
        Assert.Equal(ResourceStatus.Unhealthy, updated.Status);
        Assert.Equal("api", updated.Name);
    }

    [Fact]
    public void ResourceInfo_Equality_WorksForRecordSemantics()
    {
        var a = new ResourceInfo("api", "Project", "http://localhost", "", 0, ResourceStatus.Healthy);
        var b = new ResourceInfo("api", "Project", "http://localhost", "", 0, ResourceStatus.Healthy);

        Assert.Equal(a, b);
    }

    [Fact]
    public void ResourceInfo_Inequality_OnStatusChange()
    {
        var a = new ResourceInfo("api", "Project", "http://localhost", "", 0, ResourceStatus.Healthy);
        var b = a with { Status = ResourceStatus.Unhealthy };

        Assert.NotEqual(a, b);
    }
}
