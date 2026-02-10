using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Integration tests for BeaconTowerService — validates beacon tower construction
/// and glass color selection per resource type and health state.
/// </summary>
public class BeaconTowerServiceIntegrationTests : IAsyncLifetime
{
    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private AspireResourceMonitor _monitor = null!;
    private BeaconTowerService _sut = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance);
        _monitor = TestResourceMonitorFactory.Create();
        _sut = new BeaconTowerService(_rcon, _monitor,
            NullLogger<BeaconTowerService>.Instance);
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
    public async Task UpdateBeaconTowers_BuildsIronBaseAndBeacon()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));
        _server.ClearCommands();

        await _sut.UpdateBeaconTowersAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("iron_block") && c.Contains("fill"));
        Assert.Contains(cmds, c => c.Contains("minecraft:beacon"));
    }

    [Fact]
    public async Task UpdateBeaconTowers_HealthyProject_BlueGlass()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Healthy));
        _server.ClearCommands();

        await _sut.UpdateBeaconTowersAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("blue_stained_glass"));
    }

    [Fact]
    public async Task UpdateBeaconTowers_HealthyContainer_PurpleGlass()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("redis", "Container", ResourceStatus.Healthy));
        _server.ClearCommands();

        await _sut.UpdateBeaconTowersAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("purple_stained_glass"));
    }

    [Fact]
    public async Task UpdateBeaconTowers_HealthyExecutable_CyanGlass()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("worker", "Executable", ResourceStatus.Healthy));
        _server.ClearCommands();

        await _sut.UpdateBeaconTowersAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("cyan_stained_glass"));
    }

    [Fact]
    public async Task UpdateBeaconTowers_HealthyUnknownType_LightBlueGlass()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("svc", "CustomThing", ResourceStatus.Healthy));
        _server.ClearCommands();

        await _sut.UpdateBeaconTowersAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("light_blue_stained_glass"));
    }

    [Fact]
    public async Task UpdateBeaconTowers_UnhealthyResource_RedGlass()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Unhealthy));
        _server.ClearCommands();

        await _sut.UpdateBeaconTowersAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("red_stained_glass"));
    }

    [Fact]
    public async Task UpdateBeaconTowers_StartingResource_YellowGlass()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Unknown));
        _server.ClearCommands();

        await _sut.UpdateBeaconTowersAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("yellow_stained_glass"));
    }

    [Fact]
    public async Task UpdateBeaconTowers_MultipleResources_BuildsAll()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Healthy),
            ("redis", "Container", ResourceStatus.Healthy),
            ("worker", "Executable", ResourceStatus.Unhealthy));
        _server.ClearCommands();

        await _sut.UpdateBeaconTowersAsync();

        var cmds = _server.GetCommands();
        // 3 resources × 3 commands each (iron base, beacon, glass) = 9
        Assert.True(cmds.Count >= 9, $"Expected at least 9 commands but got {cmds.Count}");
        Assert.Contains(cmds, c => c.Contains("blue_stained_glass"));
        Assert.Contains(cmds, c => c.Contains("purple_stained_glass"));
        Assert.Contains(cmds, c => c.Contains("red_stained_glass"));
    }

    // Unit tests for GetGlassBlock (internal static method)
    [Theory]
    [InlineData("Project", ResourceStatus.Healthy, "blue_stained_glass")]
    [InlineData("Container", ResourceStatus.Healthy, "purple_stained_glass")]
    [InlineData("Executable", ResourceStatus.Healthy, "cyan_stained_glass")]
    [InlineData("Unknown", ResourceStatus.Healthy, "light_blue_stained_glass")]
    [InlineData("Project", ResourceStatus.Unhealthy, "red_stained_glass")]
    [InlineData("Container", ResourceStatus.Unhealthy, "red_stained_glass")]
    [InlineData("Project", ResourceStatus.Unknown, "yellow_stained_glass")]
    public void GetGlassBlock_ReturnsCorrectBlock(string type, ResourceStatus status, string expectedGlass)
    {
        var info = new ResourceInfo("test", type, "", "", 0, status);
        var glass = BeaconTowerService.GetGlassBlock(info);
        Assert.Equal(expectedGlass, glass);
    }

    [Fact]
    public void GetGlassBlock_CaseInsensitiveType()
    {
        var info = new ResourceInfo("test", "project", "", "", 0, ResourceStatus.Healthy);
        Assert.Equal("blue_stained_glass", BeaconTowerService.GetGlassBlock(info));

        var info2 = new ResourceInfo("test", "PROJECT", "", "", 0, ResourceStatus.Healthy);
        Assert.Equal("blue_stained_glass", BeaconTowerService.GetGlassBlock(info2));
    }
}
