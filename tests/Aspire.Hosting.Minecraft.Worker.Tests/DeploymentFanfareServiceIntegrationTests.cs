using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Integration tests for DeploymentFanfareService — validates fanfare
/// (lightning, fireworks, title) on first healthy state (Unknown → Healthy).
/// </summary>
public class DeploymentFanfareServiceIntegrationTests : IAsyncLifetime
{
    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private DeploymentFanfareService _sut = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance);
        _sut = new DeploymentFanfareService(_rcon,
            NullLogger<DeploymentFanfareService>.Instance);
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
    public async Task CheckAndCelebrate_UnknownToHealthy_PlaysFanfare()
    {
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Unknown, ResourceStatus.Healthy)
        };
        _server.ClearCommands();

        await _sut.CheckAndCelebrateAsync(changes);

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("lightning_bolt"));
        Assert.Contains(cmds, c => c.Contains("firework_rocket"));
        Assert.Contains(cmds, c => c.Contains("title @a title") && c.Contains("DEPLOYED"));
        Assert.Contains(cmds, c => c.Contains("title @a subtitle") && c.Contains("api is online"));
    }

    [Fact]
    public async Task CheckAndCelebrate_HealthyToUnhealthy_NoFanfare()
    {
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        };
        _server.ClearCommands();

        await _sut.CheckAndCelebrateAsync(changes);

        var cmds = _server.GetCommands();
        Assert.DoesNotContain(cmds, c => c.Contains("lightning_bolt"));
        Assert.DoesNotContain(cmds, c => c.Contains("DEPLOYED"));
    }

    [Fact]
    public async Task CheckAndCelebrate_UnhealthyToHealthy_NoFanfare()
    {
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Unhealthy, ResourceStatus.Healthy)
        };
        _server.ClearCommands();

        await _sut.CheckAndCelebrateAsync(changes);

        var cmds = _server.GetCommands();
        Assert.DoesNotContain(cmds, c => c.Contains("lightning_bolt"));
    }

    [Fact]
    public async Task CheckAndCelebrate_EmptyChanges_NoCommands()
    {
        _server.ClearCommands();
        await _sut.CheckAndCelebrateAsync(new List<ResourceStatusChange>());

        var cmds = _server.GetCommands();
        Assert.Empty(cmds);
    }

    [Fact]
    public async Task CheckAndCelebrate_MultipleDeployments_FanfareForEach()
    {
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Unknown, ResourceStatus.Healthy),
            new("db", "Container", ResourceStatus.Unknown, ResourceStatus.Healthy)
        };
        _server.ClearCommands();

        await _sut.CheckAndCelebrateAsync(changes);

        var cmds = _server.GetCommands();
        // Each deployment: lightning + 2 fireworks + times + title + subtitle = 6
        var lightningCmds = cmds.Count(c => c.Contains("lightning_bolt"));
        Assert.Equal(2, lightningCmds);
        Assert.Contains(cmds, c => c.Contains("api is online"));
        Assert.Contains(cmds, c => c.Contains("db is online"));
    }

    [Fact]
    public async Task CheckAndCelebrate_SetsTitleTiming()
    {
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Unknown, ResourceStatus.Healthy)
        };
        _server.ClearCommands();

        await _sut.CheckAndCelebrateAsync(changes);

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("title @a times 10 40 10"));
    }
}
