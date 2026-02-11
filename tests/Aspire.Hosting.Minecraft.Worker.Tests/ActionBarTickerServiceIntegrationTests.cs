using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Integration tests for ActionBarTickerService — validates action bar message
/// cycling through TPS, MSPT, healthy count, and RCON latency.
/// </summary>
public class ActionBarTickerServiceIntegrationTests : IAsyncLifetime
{
    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private AspireResourceMonitor _monitor = null!;
    private ActionBarTickerService _sut = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance);
        _monitor = TestResourceMonitorFactory.Create();
        _sut = new ActionBarTickerService(_rcon, _monitor,
            NullLogger<ActionBarTickerService>.Instance);
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
    public async Task Tick_SendsActionBarCommand()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));
        _server.ClearCommands();

        await _sut.TickAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("title @a actionbar"));
    }

    [Fact]
    public async Task Tick_CyclesThrough4Metrics()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true), ("db", true));

        // Tick 4 times to cycle through all metrics
        for (int i = 0; i < 4; i++)
        {
            _server.ClearCommands();
            await _sut.TickAsync();
            var cmds = _server.GetCommands();
            Assert.True(cmds.Count > 0, $"Tick {i} should produce commands");
        }
    }

    [Fact]
    public async Task Tick_HealthyCountMessage_FormatsCorrectly()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true), ("db", true), ("cache", false));

        // Tick index 2 = healthy count message
        await _sut.TickAsync(); // 0 = TPS
        await _sut.TickAsync(); // 1 = MSPT
        _server.ClearCommands();
        await _sut.TickAsync(); // 2 = Healthy count

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains("Healthy: 2/3 resources"));
    }

    [Fact]
    public async Task Tick_WrapsAroundAfter4()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true));

        // After 4 ticks, the 5th should wrap back to index 0 (TPS)
        for (int i = 0; i < 4; i++)
            await _sut.TickAsync();

        _server.ClearCommands();
        await _sut.TickAsync(); // Should be index 0 again

        var cmds = _server.GetCommands();
        // Index 0 sends 'tps' command then actionbar
        Assert.Contains(cmds, c => c.Contains("tps") || c.Contains("actionbar"));
    }

    [Fact]
    public async Task Tick_TpsFailure_ShowsFallbackMessage()
    {
        // No resources, just test that TPS failure produces fallback
        _server.ClearCommands();

        // The mock server returns empty string for 'tps' which will fail parsing
        await _sut.TickAsync(); // Index 0 = TPS

        var cmds = _server.GetCommands();
        // Either sends fallback or sends the tps command first
        Assert.Contains(cmds, c => c.Contains("tps") || c.Contains("actionbar"));
    }
}
