using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Integration tests for BossBarService — exercises the actual service class
/// against a mock RCON server, validating real command sequences and state tracking.
/// </summary>
public class BossBarServiceIntegrationTests : IAsyncLifetime
{
    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private AspireResourceMonitor _monitor = null!;
    private BossBarService _sut = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance);
        _monitor = TestResourceMonitorFactory.Create();

        _sut = new BossBarService(_rcon, _monitor,
            NullLogger<BossBarService>.Instance);

        // Wait for connection
        await WaitForRconConnected();
    }

    public async Task DisposeAsync()
    {
        await _rcon.DisposeAsync();
        await _server.DisposeAsync();
    }

    private async Task WaitForRconConnected()
    {
        // Warm up the connection by sending a command
        for (int i = 0; i < 10; i++)
        {
            try
            {
                await _rcon.SendCommandAsync("list");
                return;
            }
            catch
            {
                await Task.Delay(100);
            }
        }
    }

    [Fact]
    public async Task UpdateBossBar_FirstCall_CreatesBarAndSetsValue()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true), ("db", true));
        _server.ClearCommands();

        await _sut.UpdateBossBarAsync();

        var cmds = _server.GetCommands();
        // First call should: add, set max, set visible, set players, set value, set name, set color
        Assert.Contains(cmds, c => c.Contains("bossbar add aspire:fleet_health"));
        Assert.Contains(cmds, c => c.Contains("bossbar set aspire:fleet_health max 100"));
        Assert.Contains(cmds, c => c.Contains("bossbar set aspire:fleet_health visible true"));
        Assert.Contains(cmds, c => c.Contains("bossbar set aspire:fleet_health players @a"));
        Assert.Contains(cmds, c => c.Contains("bossbar set aspire:fleet_health value 100"));
        Assert.Contains(cmds, c => c.Contains("bossbar set aspire:fleet_health color green"));
        Assert.Contains(cmds, c => c.Contains("Fleet Health"));
    }

    [Fact]
    public async Task UpdateBossBar_SecondCall_DoesNotRecreateBar()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));

        // First call creates the bar
        await _sut.UpdateBossBarAsync();
        _server.ClearCommands();

        // Second call should NOT re-add
        await _sut.UpdateBossBarAsync();

        var cmds = _server.GetCommands();
        Assert.DoesNotContain(cmds, c => c.Contains("bossbar add"));
    }

    [Fact]
    public async Task UpdateBossBar_AlwaysResendPlayers()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));

        await _sut.UpdateBossBarAsync();
        _server.ClearCommands();

        // Second call should still send players @a
        await _sut.UpdateBossBarAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("bossbar set aspire:fleet_health players @a"));
    }

    [Fact]
    public async Task UpdateBossBar_HealthChange_UpdatesValueAndColor()
    {
        // Start with all healthy
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true), ("db", true));
        await _sut.UpdateBossBarAsync();
        _server.ClearCommands();

        // Take one down — 50% health
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true), ("db", false));
        await _sut.UpdateBossBarAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("bossbar set aspire:fleet_health value 50"));
        Assert.Contains(cmds, c => c.Contains("bossbar set aspire:fleet_health color yellow"));
    }

    [Fact]
    public async Task UpdateBossBar_AllDown_ShowsRedZeroPercent()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false), ("db", false));
        await _sut.UpdateBossBarAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("bossbar set aspire:fleet_health value 0"));
        Assert.Contains(cmds, c => c.Contains("bossbar set aspire:fleet_health color red"));
    }

    [Fact]
    public async Task UpdateBossBar_ZeroResources_DoesNothing()
    {
        // Empty monitor — no resources
        _server.ClearCommands();
        await _sut.UpdateBossBarAsync();

        var cmds = _server.GetCommands();
        Assert.Empty(cmds);
    }

    [Fact]
    public async Task UpdateBossBar_SameHealth_DoesNotResendValue()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));
        await _sut.UpdateBossBarAsync();
        _server.ClearCommands();

        // Same health — value/name should NOT be re-sent
        await _sut.UpdateBossBarAsync();

        var cmds = _server.GetCommands();
        Assert.DoesNotContain(cmds, c => c.Contains("bossbar set aspire:fleet_health value"));
        Assert.DoesNotContain(cmds, c => c.Contains("bossbar set aspire:fleet_health name"));
    }

    [Fact]
    public async Task UpdateBossBar_ColorThresholds_100IsGreen()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("a", true), ("b", true), ("c", true), ("d", true));
        await _sut.UpdateBossBarAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("color green"));
    }

    [Fact]
    public async Task UpdateBossBar_ColorThresholds_49IsRed()
    {
        // 1 out of 3 healthy = 33% → red (< 50)
        TestResourceMonitorFactory.SetResources(_monitor, ("a", true), ("b", false), ("c", false));
        await _sut.UpdateBossBarAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("color red"));
    }

    [Fact]
    public async Task UpdateBossBar_ColorThresholds_50IsYellow()
    {
        // 2 out of 4 = 50% → yellow
        TestResourceMonitorFactory.SetResources(_monitor, ("a", true), ("b", true), ("c", false), ("d", false));
        await _sut.UpdateBossBarAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("color yellow"));
    }
}
