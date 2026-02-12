using Aspire.Hosting.Minecraft.Worker.Services;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Services;

public class HealthHistoryTrackerTests
{
    [Fact]
    public void Record_SingleEntry_ReturnsOne()
    {
        var tracker = new HealthHistoryTracker();

        tracker.Record("api", ResourceStatus.Healthy);

        var history = tracker.GetHistory("api");
        Assert.Single(history);
        Assert.Equal(ResourceStatus.Healthy, history[0]);
    }

    [Fact]
    public void Record_MultipleEntries_ReturnsInOrder()
    {
        var tracker = new HealthHistoryTracker();

        tracker.Record("api", ResourceStatus.Healthy);
        tracker.Record("api", ResourceStatus.Unhealthy);
        tracker.Record("api", ResourceStatus.Unknown);

        var history = tracker.GetHistory("api");
        Assert.Equal(3, history.Count);
        Assert.Equal(ResourceStatus.Healthy, history[0]);
        Assert.Equal(ResourceStatus.Unhealthy, history[1]);
        Assert.Equal(ResourceStatus.Unknown, history[2]);
    }

    [Fact]
    public void Record_OverCapacity_EvictsOldest()
    {
        var tracker = new HealthHistoryTracker(capacity: 3);

        tracker.Record("api", ResourceStatus.Healthy);
        tracker.Record("api", ResourceStatus.Unhealthy);
        tracker.Record("api", ResourceStatus.Unknown);
        tracker.Record("api", ResourceStatus.Healthy); // evicts first Healthy

        var history = tracker.GetHistory("api");
        Assert.Equal(3, history.Count);
        Assert.Equal(ResourceStatus.Unhealthy, history[0]);
        Assert.Equal(ResourceStatus.Unknown, history[1]);
        Assert.Equal(ResourceStatus.Healthy, history[2]);
    }

    [Fact]
    public void GetHistory_UnknownResource_ReturnsEmpty()
    {
        var tracker = new HealthHistoryTracker();

        var history = tracker.GetHistory("nonexistent");

        Assert.Empty(history);
    }

    [Fact]
    public void GetAllResources_ReturnsTrackedNames()
    {
        var tracker = new HealthHistoryTracker();

        tracker.Record("api", ResourceStatus.Healthy);
        tracker.Record("redis", ResourceStatus.Unhealthy);
        tracker.Record("worker", ResourceStatus.Unknown);

        var resources = tracker.GetAllResources();
        Assert.Equal(3, resources.Count);
        Assert.Contains("api", resources);
        Assert.Contains("redis", resources);
        Assert.Contains("worker", resources);
    }

    [Fact]
    public void Record_ThreadSafe_NoConcurrencyErrors()
    {
        var tracker = new HealthHistoryTracker(capacity: 100);
        var statuses = new[] { ResourceStatus.Healthy, ResourceStatus.Unhealthy, ResourceStatus.Unknown };

        Parallel.For(0, 1000, i =>
        {
            var resource = $"resource-{i % 10}";
            tracker.Record(resource, statuses[i % statuses.Length]);
        });

        var resources = tracker.GetAllResources();
        Assert.Equal(10, resources.Count);

        foreach (var name in resources)
        {
            var history = tracker.GetHistory(name);
            Assert.True(history.Count > 0);
            Assert.True(history.Count <= 100);
        }
    }
}
