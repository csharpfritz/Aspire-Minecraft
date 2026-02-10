using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Integration tests for TitleAlertService — validates real title RCON commands
/// are sent for health transitions.
/// </summary>
public class TitleAlertServiceIntegrationTests : IAsyncLifetime
{
    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private TitleAlertService _sut = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance);

        _sut = new TitleAlertService(_rcon, NullLogger<TitleAlertService>.Instance);

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
    public async Task ShowTitleAlerts_EmptyChanges_SendsNoCommands()
    {
        _server.ClearCommands();
        await _sut.ShowTitleAlertsAsync([]);

        var cmds = _server.GetCommands();
        Assert.Empty(cmds);
    }

    [Fact]
    public async Task ShowTitleAlerts_UnhealthyTransition_SendsRedTitle()
    {
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        };
        _server.ClearCommands();

        await _sut.ShowTitleAlertsAsync(changes);

        var cmds = _server.GetCommands();
        // Should set times, then title + subtitle
        Assert.Contains(cmds, c => c.Contains("title @a times"));
        Assert.Contains(cmds, c => c.Contains("title @a title") && c.Contains("red"));
        Assert.Contains(cmds, c => c.Contains("title @a subtitle") && c.Contains("api"));
    }

    [Fact]
    public async Task ShowTitleAlerts_HealthyTransition_SendsGreenTitle()
    {
        var changes = new List<ResourceStatusChange>
        {
            new("db", "Postgres", ResourceStatus.Unhealthy, ResourceStatus.Healthy)
        };
        _server.ClearCommands();

        await _sut.ShowTitleAlertsAsync(changes);

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("title @a title") && c.Contains("green"));
        Assert.Contains(cmds, c => c.Contains("title @a subtitle") && c.Contains("db"));
    }

    [Fact]
    public async Task ShowTitleAlerts_MultipleChanges_SendsTitleForEach()
    {
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy),
            new("db", "Postgres", ResourceStatus.Unhealthy, ResourceStatus.Healthy)
        };
        _server.ClearCommands();

        await _sut.ShowTitleAlertsAsync(changes);

        var cmds = _server.GetCommands();
        // times (once) + 2 titles + 2 subtitles = 5
        Assert.Contains(cmds, c => c.Contains("title @a times"));
        Assert.Contains(cmds, c => c.Contains("api"));
        Assert.Contains(cmds, c => c.Contains("db"));
    }

    [Fact]
    public async Task ShowTitleAlerts_Times_UsesCorrectValues()
    {
        var changes = new List<ResourceStatusChange>
        {
            new("svc", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        };
        _server.ClearCommands();

        await _sut.ShowTitleAlertsAsync(changes);

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c == "title @a times 10 60 20");
    }

    [Fact]
    public async Task ShowTitleAlerts_UnhealthyTitle_ContainsServiceDownText()
    {
        var changes = new List<ResourceStatusChange>
        {
            new("redis", "Container", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        };
        _server.ClearCommands();

        await _sut.ShowTitleAlertsAsync(changes);

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("SERVICE DOWN"));
    }

    [Fact]
    public async Task ShowTitleAlerts_HealthyTitle_ContainsBackOnlineText()
    {
        var changes = new List<ResourceStatusChange>
        {
            new("redis", "Container", ResourceStatus.Unhealthy, ResourceStatus.Healthy)
        };
        _server.ClearCommands();

        await _sut.ShowTitleAlertsAsync(changes);

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("BACK ONLINE"));
    }
}
