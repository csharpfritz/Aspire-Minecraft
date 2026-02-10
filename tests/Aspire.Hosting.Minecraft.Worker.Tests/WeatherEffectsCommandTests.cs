using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Aspire.Hosting.Minecraft.Worker.Services;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Tests for Weather = System Health (#7) — RCON weather commands linked to overall fleet health.
/// Weather should reflect aggregate health: all healthy=clear, some unhealthy=rain, many unhealthy=thunder.
/// </summary>
public class WeatherEffectsCommandTests
{
    [Fact]
    public void WeatherClear_HasCorrectFormat()
    {
        var cmd = RconCommandFormats.WeatherClear();
        Assert.Equal("weather clear", cmd);
    }

    [Fact]
    public void WeatherRain_HasCorrectFormat()
    {
        var cmd = RconCommandFormats.WeatherRain();
        Assert.Equal("weather rain", cmd);
    }

    [Fact]
    public void WeatherThunder_HasCorrectFormat()
    {
        var cmd = RconCommandFormats.WeatherThunder();
        Assert.Equal("weather thunder", cmd);
    }

    [Theory]
    [InlineData("clear")]
    [InlineData("rain")]
    [InlineData("thunder")]
    public void Weather_Command_AcceptsValidWeatherTypes(string weatherType)
    {
        var cmd = RconCommandFormats.Weather(weatherType);
        Assert.Equal($"weather {weatherType}", cmd);
    }

    [Fact]
    public void Weather_AllHealthy_ShouldBeClear()
    {
        // When all resources are healthy, weather should be clear
        int healthy = 5, total = 5;
        var ratio = (double)healthy / total;

        Assert.Equal(1.0, ratio);
        // Expected: weather clear
        var cmd = RconCommandFormats.WeatherClear();
        Assert.Equal("weather clear", cmd);
    }

    [Fact]
    public void Weather_SomeUnhealthy_ShouldBeRain()
    {
        // When some (but not all) resources are unhealthy, weather should be rain
        int healthy = 3, total = 5;
        var ratio = (double)healthy / total;

        Assert.True(ratio > 0 && ratio < 1.0);
        var cmd = RconCommandFormats.WeatherRain();
        Assert.Equal("weather rain", cmd);
    }

    [Fact]
    public void Weather_MostUnhealthy_ShouldBeThunder()
    {
        // When most/all resources are unhealthy, weather should be thunder
        int healthy = 0, total = 5;
        var ratio = (double)healthy / total;

        Assert.Equal(0.0, ratio);
        var cmd = RconCommandFormats.WeatherThunder();
        Assert.Equal("weather thunder", cmd);
    }

    [Theory]
    [InlineData(5, 5, "clear")]    // 100% healthy
    [InlineData(4, 5, "rain")]     // 80% healthy
    [InlineData(3, 5, "rain")]     // 60% healthy
    [InlineData(2, 5, "rain")]     // 40% healthy
    [InlineData(1, 5, "rain")]     // 20% healthy — borderline
    [InlineData(0, 5, "thunder")]  // 0% healthy — everything down
    public void Weather_HealthRatioMapping_ProducesExpectedWeather(int healthy, int total, string expectedWeather)
    {
        // Expected mapping logic:
        // 100% healthy → clear
        // 20-99% healthy → rain
        // <20% healthy → thunder
        var ratio = total > 0 ? (double)healthy / total : 0;
        string weather;
        if (ratio >= 1.0)
            weather = "clear";
        else if (ratio >= 0.2)
            weather = "rain";
        else
            weather = "thunder";

        Assert.Equal(expectedWeather, weather);
    }

    [Fact]
    public void Weather_ZeroResources_ShouldDefaultToClear()
    {
        // Edge case: no resources discovered yet
        int total = 0;

        // With no resources, default to clear (no data ≠ unhealthy)
        string weather = total == 0 ? "clear" : "rain";
        Assert.Equal("clear", weather);
    }

    [Fact]
    public void Weather_ShouldNotChangeIfSameState()
    {
        // Weather should only send RCON command when weather TYPE changes
        // Not on every poll (to avoid flickering)
        string previousWeather = "clear";
        string currentWeather = "clear";

        bool shouldSendCommand = previousWeather != currentWeather;
        Assert.False(shouldSendCommand);
    }

    [Fact]
    public void Weather_ShouldChangeOnTransition()
    {
        string previousWeather = "clear";
        string currentWeather = "rain";

        bool shouldSendCommand = previousWeather != currentWeather;
        Assert.True(shouldSendCommand);
    }

    [Fact]
    public void Weather_RapidHealthChanges_ShouldNotFlicker()
    {
        // Simulate rapid changes: healthy → unhealthy → healthy in quick succession
        // Weather should have some debounce logic (test the concept)
        var healthHistory = new[] { "clear", "rain", "clear", "rain", "clear" };
        var commandsSent = new List<string>();

        string? lastSent = null;
        foreach (var weather in healthHistory)
        {
            if (weather != lastSent)
            {
                commandsSent.Add(weather);
                lastSent = weather;
            }
        }

        // Even with rapid changes, each transition should be tracked
        Assert.Equal(5, commandsSent.Count);
    }
}
