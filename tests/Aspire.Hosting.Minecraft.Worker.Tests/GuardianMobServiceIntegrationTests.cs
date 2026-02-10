using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Integration tests for GuardianMobService — validates mob spawn/despawn
/// RCON commands for healthy/unhealthy resource transitions.
/// </summary>
public class GuardianMobServiceIntegrationTests : IAsyncLifetime
{
    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private AspireResourceMonitor _monitor = null!;
    private GuardianMobService _sut = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance);
        _monitor = TestResourceMonitorFactory.Create();
        _sut = new GuardianMobService(_rcon, _monitor,
            NullLogger<GuardianMobService>.Instance);
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
    public async Task UpdateGuardianMobs_HealthyResource_SpawnsIronGolem()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));
        _server.ClearCommands();

        await _sut.UpdateGuardianMobsAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("kill @e[name=guardian_api]"));
        Assert.Contains(cmds, c => c.Contains("summon minecraft:iron_golem") && c.Contains("guardian_api"));
    }

    [Fact]
    public async Task UpdateGuardianMobs_UnhealthyResource_SpawnsZombie()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false));
        _server.ClearCommands();

        await _sut.UpdateGuardianMobsAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("kill @e[name=guardian_api]"));
        Assert.Contains(cmds, c => c.Contains("summon minecraft:zombie") && c.Contains("guardian_api"));
    }

    [Fact]
    public async Task UpdateGuardianMobs_SameStatusTwice_DoesNotRespawn()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));
        await _sut.UpdateGuardianMobsAsync();
        _server.ClearCommands();

        await _sut.UpdateGuardianMobsAsync();

        var cmds = _server.GetCommands();
        Assert.DoesNotContain(cmds, c => c.Contains("summon"));
        Assert.DoesNotContain(cmds, c => c.Contains("kill"));
    }

    [Fact]
    public async Task UpdateGuardianMobs_StatusChange_Respawns()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));
        await _sut.UpdateGuardianMobsAsync();

        // Change to unhealthy
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false));
        _server.ClearCommands();
        await _sut.UpdateGuardianMobsAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("kill @e[name=guardian_api]"));
        Assert.Contains(cmds, c => c.Contains("summon minecraft:zombie"));
    }

    [Fact]
    public async Task UpdateGuardianMobs_Recovery_SpawnsGolemAgain()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false));
        await _sut.UpdateGuardianMobsAsync();

        // Recover
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));
        _server.ClearCommands();
        await _sut.UpdateGuardianMobsAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("summon minecraft:iron_golem") && c.Contains("guardian_api"));
    }

    [Fact]
    public async Task UpdateGuardianMobs_MultipleResources_SpawnsSeparateMobs()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true), ("db", false));
        _server.ClearCommands();

        await _sut.UpdateGuardianMobsAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("guardian_api") && c.Contains("iron_golem"));
        Assert.Contains(cmds, c => c.Contains("guardian_db") && c.Contains("zombie"));
    }

    [Fact]
    public async Task UpdateGuardianMobs_KillsExistingBeforeSpawning()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));
        _server.ClearCommands();

        await _sut.UpdateGuardianMobsAsync();

        var cmds = _server.GetCommands();
        var killIdx = cmds.FindIndex(c => c.Contains("kill @e[name=guardian_api]"));
        var summonIdx = cmds.FindIndex(c => c.Contains("summon"));
        Assert.True(killIdx >= 0, "Expected kill command");
        Assert.True(summonIdx > killIdx, "Kill should precede summon");
    }

    [Fact]
    public async Task UpdateGuardianMobs_MobsAreInvulnerableAndNoAI()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));
        _server.ClearCommands();

        await _sut.UpdateGuardianMobsAsync();

        var cmds = _server.GetCommands();
        var summonCmd = cmds.First(c => c.Contains("summon"));
        Assert.Contains("NoAI:1b", summonCmd);
        Assert.Contains("Invulnerable:1b", summonCmd);
        Assert.Contains("PersistenceRequired:1b", summonCmd);
    }
}
