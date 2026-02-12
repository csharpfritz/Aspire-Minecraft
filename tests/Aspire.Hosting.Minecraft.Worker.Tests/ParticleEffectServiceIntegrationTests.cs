using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Integration tests for ParticleEffectService — validates real particle RCON commands
/// are sent at correct coordinates for health transitions.
/// </summary>
public class ParticleEffectServiceIntegrationTests : IAsyncLifetime
{
    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private AspireResourceMonitor _monitor = null!;
    private ParticleEffectService _sut = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance);
        _monitor = TestResourceMonitorFactory.Create();

        _sut = new ParticleEffectService(_rcon, _monitor,
            NullLogger<ParticleEffectService>.Instance);

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
    public async Task ShowParticles_EmptyChanges_SendsNoCommands()
    {
        _server.ClearCommands();
        await _sut.ShowParticlesForChangesAsync([]);

        var cmds = _server.GetCommands();
        Assert.Empty(cmds);
    }

    [Fact]
    public async Task ShowParticles_Unhealthy_SendsSmokeAndFlame()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false));
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        };
        _server.ClearCommands();

        await _sut.ShowParticlesForChangesAsync(changes);

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("large_smoke"));
        Assert.Contains(cmds, c => c.Contains("flame"));
    }

    [Fact]
    public async Task ShowParticles_Healthy_SendsHappyVillager()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Unhealthy, ResourceStatus.Healthy)
        };
        _server.ClearCommands();

        await _sut.ShowParticlesForChangesAsync(changes);

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("happy_villager"));
    }

    [Fact]
    public async Task ShowParticles_CoordinatesMatchResourceIndex()
    {
        // Set up two resources
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false), ("db", false));
        var changes = new List<ResourceStatusChange>
        {
            new("db", "Postgres", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        };
        _server.ClearCommands();

        await _sut.ShowParticlesForChangesAsync(changes);

        var cmds = _server.GetCommands();
        // "db" is at index 1 → VillageLayout: col=1, row=0 → center x=37, y=-50, z=3
        Assert.Contains(cmds, c => c.Contains("37 -50 3"));
    }

    [Fact]
    public async Task ShowParticles_FirstResource_HasCorrectCoordinates()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false));
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        };
        _server.ClearCommands();

        await _sut.ShowParticlesForChangesAsync(changes);

        var cmds = _server.GetCommands();
        // "api" is at index 0 → VillageLayout: col=0, row=0 → center x=13, y=-50, z=3
        Assert.Contains(cmds, c => c.Contains("13 -50 3"));
    }

    [Fact]
    public async Task ShowParticles_UnknownResource_IsSkipped()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));
        var changes = new List<ResourceStatusChange>
        {
            new("unknown-svc", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        };
        _server.ClearCommands();

        await _sut.ShowParticlesForChangesAsync(changes);

        var cmds = _server.GetCommands();
        Assert.Empty(cmds);
    }

    [Fact]
    public async Task ShowParticles_MultipleChanges_SendsParticlesForEach()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false), ("db", true));
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy),
            new("db", "Postgres", ResourceStatus.Unhealthy, ResourceStatus.Healthy)
        };
        _server.ClearCommands();

        await _sut.ShowParticlesForChangesAsync(changes);

        var cmds = _server.GetCommands();
        // api unhealthy: 2 commands (smoke + flame), db healthy: 1 command (happy_villager)
        Assert.Contains(cmds, c => c.Contains("large_smoke"));
        Assert.Contains(cmds, c => c.Contains("flame"));
        Assert.Contains(cmds, c => c.Contains("happy_villager"));
    }

    [Fact]
    public async Task ShowParticles_UnhealthyUsesForceMode()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false));
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        };
        _server.ClearCommands();

        await _sut.ShowParticlesForChangesAsync(changes);

        var cmds = _server.GetCommands();
        Assert.All(cmds, c => Assert.Contains("force", c));
    }
}
