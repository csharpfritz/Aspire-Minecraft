using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Integration tests for FireworksService — validates fireworks launch on full recovery
/// from an unhealthy state.
/// </summary>
public class FireworksServiceIntegrationTests : IAsyncLifetime
{
    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private AspireResourceMonitor _monitor = null!;
    private FireworksService _sut = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance);
        _monitor = TestResourceMonitorFactory.Create();
        _sut = new FireworksService(_rcon, _monitor,
            NullLogger<FireworksService>.Instance);
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
    public async Task CheckAndLaunchFireworks_AllHealthyFromStart_NoFireworks()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true), ("db", true));
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Unknown, ResourceStatus.Healthy)
        };
        _server.ClearCommands();

        await _sut.CheckAndLaunchFireworksAsync(changes);

        var cmds = _server.GetCommands();
        Assert.DoesNotContain(cmds, c => c.Contains("firework_rocket"));
    }

    [Fact]
    public async Task CheckAndLaunchFireworks_RecoverFromUnhealthy_LaunchesFireworks()
    {
        // First: mark as unhealthy
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false));
        await _sut.CheckAndLaunchFireworksAsync(new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        });

        // Now recover all
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));
        _server.ClearCommands();
        await _sut.CheckAndLaunchFireworksAsync(new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Unhealthy, ResourceStatus.Healthy)
        });

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("firework_rocket"));
    }

    [Fact]
    public async Task CheckAndLaunchFireworks_PartialRecovery_NoFireworks()
    {
        // Mark unhealthy
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false), ("db", false));
        await _sut.CheckAndLaunchFireworksAsync(new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        });

        // Only one recovers
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true), ("db", false));
        _server.ClearCommands();
        await _sut.CheckAndLaunchFireworksAsync(new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Unhealthy, ResourceStatus.Healthy)
        });

        var cmds = _server.GetCommands();
        Assert.DoesNotContain(cmds, c => c.Contains("firework_rocket"));
    }

    [Fact]
    public async Task CheckAndLaunchFireworks_MultiplePositions()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false));
        await _sut.CheckAndLaunchFireworksAsync(new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        });

        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));
        _server.ClearCommands();
        await _sut.CheckAndLaunchFireworksAsync(new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Unhealthy, ResourceStatus.Healthy)
        });

        var cmds = _server.GetCommands();
        var fireworkCmds = cmds.Where(c => c.Contains("firework_rocket")).ToList();
        Assert.Equal(5, fireworkCmds.Count);
    }

    [Fact]
    public async Task CheckAndLaunchFireworks_SecondRecovery_RequiresNewUnhealthy()
    {
        // First cycle: unhealthy → recovery → fireworks
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false));
        await _sut.CheckAndLaunchFireworksAsync(new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        });
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));
        await _sut.CheckAndLaunchFireworksAsync(new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Unhealthy, ResourceStatus.Healthy)
        });

        // Second cycle: still all healthy — no fireworks (wasAnyUnhealthy was reset)
        _server.ClearCommands();
        await _sut.CheckAndLaunchFireworksAsync(new List<ResourceStatusChange>());

        var cmds = _server.GetCommands();
        Assert.DoesNotContain(cmds, c => c.Contains("firework_rocket"));
    }

    [Fact]
    public async Task CheckAndLaunchFireworks_EmptyChanges_NoFireworks()
    {
        _server.ClearCommands();
        await _sut.CheckAndLaunchFireworksAsync(new List<ResourceStatusChange>());

        var cmds = _server.GetCommands();
        Assert.Empty(cmds);
    }
}
