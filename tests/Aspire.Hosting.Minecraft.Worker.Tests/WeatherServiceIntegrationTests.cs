using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Integration tests for WeatherService — exercises actual service class
/// against a mock RCON server, validating weather commands and state transitions.
/// </summary>
public class WeatherServiceIntegrationTests : IAsyncLifetime
{
    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private AspireResourceMonitor _monitor = null!;
    private WeatherService _sut = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance);
        _monitor = TestResourceMonitorFactory.Create();

        _sut = new WeatherService(_rcon, _monitor,
            NullLogger<WeatherService>.Instance);

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
            try
            {
                await _rcon.SendCommandAsync("list");
                return;
            }
            catch { await Task.Delay(100); }
        }
    }

    [Fact]
    public async Task UpdateWeather_AllHealthy_SendsClear()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true), ("db", true));
        _server.ClearCommands();

        await _sut.UpdateWeatherAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c == "weather clear");
    }

    [Fact]
    public async Task UpdateWeather_HalfHealthy_SendsRain()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true), ("db", false));
        _server.ClearCommands();

        await _sut.UpdateWeatherAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c == "weather rain");
    }

    [Fact]
    public async Task UpdateWeather_AllDown_SendsThunder()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false), ("db", false));
        _server.ClearCommands();

        await _sut.UpdateWeatherAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c == "weather thunder");
    }

    [Fact]
    public async Task UpdateWeather_SameState_DoesNotResend()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true), ("db", true));
        await _sut.UpdateWeatherAsync();
        _server.ClearCommands();

        // Same state — no command
        await _sut.UpdateWeatherAsync();

        var cmds = _server.GetCommands();
        Assert.Empty(cmds);
    }

    [Fact]
    public async Task UpdateWeather_Transition_ClearToRain_SendsOnce()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true), ("db", true));
        await _sut.UpdateWeatherAsync();
        _server.ClearCommands();

        // Degrade
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true), ("db", false));
        await _sut.UpdateWeatherAsync();

        var cmds = _server.GetCommands();
        Assert.Single(cmds);
        Assert.Equal("weather rain", cmds[0]);
    }

    [Fact]
    public async Task UpdateWeather_ZeroResources_DoesNothing()
    {
        // No resources — early exit
        _server.ClearCommands();
        await _sut.UpdateWeatherAsync();

        var cmds = _server.GetCommands();
        Assert.Empty(cmds);
    }

    [Fact]
    public async Task UpdateWeather_ThunderToRain_OnPartialRecovery()
    {
        // All down → thunder
        TestResourceMonitorFactory.SetResources(_monitor, ("a", false), ("b", false));
        await _sut.UpdateWeatherAsync();
        _server.ClearCommands();

        // Partial recovery → rain (1/2 = 50%)
        TestResourceMonitorFactory.SetResources(_monitor, ("a", true), ("b", false));
        await _sut.UpdateWeatherAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c == "weather rain");
    }

    [Fact]
    public async Task UpdateWeather_ThunderToClear_OnFullRecovery()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("a", false), ("b", false));
        await _sut.UpdateWeatherAsync();
        _server.ClearCommands();

        // Full recovery
        TestResourceMonitorFactory.SetResources(_monitor, ("a", true), ("b", true));
        await _sut.UpdateWeatherAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c == "weather clear");
    }
}
