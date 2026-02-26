using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Services;

/// <summary>
/// Integration tests for MinecartRailService verifying rail placement between dependent resources,
/// health-reactive rail enable/disable, and chest minecart spawning.
/// </summary>
public class MinecartRailServiceTests : IAsyncLifetime
{
    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private AspireResourceMonitor _monitor = null!;
    private MinecartRailService _railService = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance, maxCommandsPerSecond: 1000);
        _monitor = TestResourceMonitorFactory.Create();
        _railService = new MinecartRailService(_rcon, _monitor,
            NullLogger<MinecartRailService>.Instance);

        await WaitForRconConnected();
    }

    public async Task DisposeAsync()
    {
        await _rcon.DisposeAsync();
        await _server.DisposeAsync();
    }

    private async Task WaitForRconConnected()
    {
        for (int i = 0; i < 10; i++)
        {
            try { await _rcon.SendCommandAsync("list"); return; }
            catch { await Task.Delay(100); }
        }
    }

    [Fact]
    public async Task InitializeAsync_NoResources_SendsNoCommands()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor);
        _server.ClearCommands();

        await _railService.InitializeAsync();

        var commands = _server.GetCommands();
        Assert.Empty(commands);
    }

    [Fact]
    public async Task InitializeAsync_TwoResourcesNoDependency_NoRails()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("service-a", "Project", ResourceStatus.Healthy),
            ("service-b", "Container", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _railService.InitializeAsync();

        var commands = _server.GetCommands();
        Assert.DoesNotContain(commands, c => c.Contains("rail"));
        Assert.DoesNotContain(commands, c => c.Contains("summon"));
    }

    [Fact]
    public async Task InitializeAsync_TwoResourcesWithDependency_PlacesRails()
    {
        TestResourceMonitorFactory.SetResourcesWithDependencies(_monitor,
            ("parent", "Project", ResourceStatus.Healthy, []),
            ("child", "Container", ResourceStatus.Healthy, ["parent"])
        );
        _server.ClearCommands();

        await _railService.InitializeAsync();

        var commands = _server.GetCommands();
        Assert.Contains(commands, c => c.Contains("minecraft:powered_rail"));
        Assert.Contains(commands, c => c.Contains("minecraft:detector_rail"));
        Assert.Contains(commands, c => c.Contains("minecraft:rail"));
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_OnlyBuildsOnce()
    {
        TestResourceMonitorFactory.SetResourcesWithDependencies(_monitor,
            ("parent", "Project", ResourceStatus.Healthy, []),
            ("child", "Container", ResourceStatus.Healthy, ["parent"])
        );
        _server.ClearCommands();

        await _railService.InitializeAsync();
        var firstCallCount = _server.GetCommands().Count;
        Assert.True(firstCallCount > 0);

        _server.ClearCommands();
        await _railService.InitializeAsync();

        var secondCallCommands = _server.GetCommands();
        Assert.Empty(secondCallCommands);
    }

    [Fact]
    public async Task UpdateAsync_ParentGoesUnhealthy_DisablesRails()
    {
        TestResourceMonitorFactory.SetResourcesWithDependencies(_monitor,
            ("parent", "Project", ResourceStatus.Healthy, []),
            ("child", "Container", ResourceStatus.Healthy, ["parent"])
        );
        await _railService.InitializeAsync();

        // Trigger initial status tracking
        await _railService.UpdateAsync();
        _server.ClearCommands();

        // Change parent to unhealthy
        TestResourceMonitorFactory.SetResourcesWithDependencies(_monitor,
            ("parent", "Project", ResourceStatus.Unhealthy, []),
            ("child", "Container", ResourceStatus.Healthy, ["parent"])
        );
        await _railService.UpdateAsync();

        var commands = _server.GetCommands();
        // Powered rails replaced with air when parent goes unhealthy
        Assert.Contains(commands, c => c.Contains("minecraft:air"));
    }

    [Fact]
    public async Task UpdateAsync_ParentRecovers_RestoresRails()
    {
        TestResourceMonitorFactory.SetResourcesWithDependencies(_monitor,
            ("parent", "Project", ResourceStatus.Healthy, []),
            ("child", "Container", ResourceStatus.Healthy, ["parent"])
        );
        await _railService.InitializeAsync();

        // Track initial healthy status
        await _railService.UpdateAsync();

        // Go unhealthy
        TestResourceMonitorFactory.SetResourcesWithDependencies(_monitor,
            ("parent", "Project", ResourceStatus.Unhealthy, []),
            ("child", "Container", ResourceStatus.Healthy, ["parent"])
        );
        await _railService.UpdateAsync();
        _server.ClearCommands();

        // Recover to healthy
        TestResourceMonitorFactory.SetResourcesWithDependencies(_monitor,
            ("parent", "Project", ResourceStatus.Healthy, []),
            ("child", "Container", ResourceStatus.Healthy, ["parent"])
        );
        await _railService.UpdateAsync();

        var commands = _server.GetCommands();
        Assert.Contains(commands, c => c.Contains("minecraft:powered_rail"));
    }

    [Fact]
    public async Task InitializeAsync_PlacesChestMinecart()
    {
        TestResourceMonitorFactory.SetResourcesWithDependencies(_monitor,
            ("parent", "Project", ResourceStatus.Healthy, []),
            ("child", "Container", ResourceStatus.Healthy, ["parent"])
        );
        _server.ClearCommands();

        await _railService.InitializeAsync();

        var commands = _server.GetCommands();
        Assert.Contains(commands, c => c.Contains("summon minecraft:chest_minecart"));
    }
}
