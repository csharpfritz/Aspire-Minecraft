using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Integration tests for SoundEffectService — validates real playsound RCON commands
/// are sent on health transitions with correct sound names.
/// </summary>
public class SoundEffectServiceIntegrationTests : IAsyncLifetime
{
    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private AspireResourceMonitor _monitor = null!;
    private SoundEffectService _sut = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance);
        _monitor = TestResourceMonitorFactory.Create();

        _sut = new SoundEffectService(_rcon, _monitor,
            NullLogger<SoundEffectService>.Instance);

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
    public async Task PlaySounds_EmptyChanges_SendsNoCommands()
    {
        _server.ClearCommands();
        await _sut.PlaySoundsForChangesAsync([]);

        var cmds = _server.GetCommands();
        Assert.Empty(cmds);
    }

    [Fact]
    public async Task PlaySounds_Unhealthy_SendsWitherAmbient()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false));
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        };
        _server.ClearCommands();

        await _sut.PlaySoundsForChangesAsync(changes);

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("entity.wither.ambient"));
    }

    [Fact]
    public async Task PlaySounds_Healthy_SendsLevelUp()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Unhealthy, ResourceStatus.Healthy)
        };
        _server.ClearCommands();

        await _sut.PlaySoundsForChangesAsync(changes);

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("entity.player.levelup"));
    }

    [Fact]
    public async Task PlaySounds_AllGreenRecovery_PlaysCelebration()
    {
        // All resources healthy after a recovery
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true), ("db", true));
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Unhealthy, ResourceStatus.Healthy)
        };
        _server.ClearCommands();

        await _sut.PlaySoundsForChangesAsync(changes);

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("entity.player.levelup"));
        Assert.Contains(cmds, c => c.Contains("ui.toast.challenge_complete"));
    }

    [Fact]
    public async Task PlaySounds_PartialRecovery_NoCelebration()
    {
        // Not all healthy — no celebration sound
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true), ("db", false));
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Unhealthy, ResourceStatus.Healthy)
        };
        _server.ClearCommands();

        await _sut.PlaySoundsForChangesAsync(changes);

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("entity.player.levelup"));
        Assert.DoesNotContain(cmds, c => c.Contains("challenge_complete"));
    }

    [Fact]
    public async Task PlaySounds_MultipleChanges_PlaysSoundForEach()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false), ("db", false));
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy),
            new("db", "Postgres", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        };
        _server.ClearCommands();

        await _sut.PlaySoundsForChangesAsync(changes);

        var cmds = _server.GetCommands();
        // 2 wither sounds (one per unhealthy change), no celebration
        var witherCount = cmds.Count(c => c.Contains("entity.wither.ambient"));
        Assert.Equal(2, witherCount);
    }

    [Fact]
    public async Task PlaySounds_AllSoundsTargetAllPlayers()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false));
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        };
        _server.ClearCommands();

        await _sut.PlaySoundsForChangesAsync(changes);

        var cmds = _server.GetCommands();
        Assert.All(cmds, c => Assert.Contains("@a", c));
    }

    [Fact]
    public async Task PlaySounds_AllSoundsUseMasterChannel()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false));
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        };
        _server.ClearCommands();

        await _sut.PlaySoundsForChangesAsync(changes);

        var cmds = _server.GetCommands();
        Assert.All(cmds, c => Assert.Contains("master", c));
    }
}
