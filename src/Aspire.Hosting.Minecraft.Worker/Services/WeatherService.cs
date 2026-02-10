using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Links Minecraft weather to overall Aspire fleet health.
/// Clear = all healthy, Rain = degraded, Thunder = critical (majority down).
/// Only changes weather on state transitions.
/// </summary>
internal sealed class WeatherService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<WeatherService> logger)
{
    private WeatherState _lastWeather = WeatherState.Unknown;

    /// <summary>
    /// Evaluates fleet health and sets weather accordingly, only on transitions.
    /// </summary>
    public async Task UpdateWeatherAsync(CancellationToken ct = default)
    {
        if (monitor.TotalCount == 0) return;

        var healthyRatio = (double)monitor.HealthyCount / monitor.TotalCount;
        var desired = healthyRatio switch
        {
            1.0 => WeatherState.Clear,
            >= 0.5 => WeatherState.Rain,
            _ => WeatherState.Thunder
        };

        if (desired == _lastWeather) return;

        var command = desired switch
        {
            WeatherState.Clear => "weather clear",
            WeatherState.Rain => "weather rain",
            WeatherState.Thunder => "weather thunder",
            _ => "weather clear"
        };

        await rcon.SendCommandAsync(command, ct);
        logger.LogInformation("Weather changed: {Old} -> {New} (healthy: {Healthy}/{Total})",
            _lastWeather, desired, monitor.HealthyCount, monitor.TotalCount);

        _lastWeather = desired;
    }

    private enum WeatherState
    {
        Unknown,
        Clear,
        Rain,
        Thunder
    }
}
