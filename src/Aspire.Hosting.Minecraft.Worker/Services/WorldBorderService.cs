using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Pulses the Minecraft world border based on Aspire fleet health.
/// Normal: 200 blocks diameter. Critical (&gt;50% unhealthy): shrinks to 100 blocks over 10 seconds
/// with a red warning tint. Restores on recovery.
/// </summary>
internal sealed class WorldBorderService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<WorldBorderService> logger)
{
    private const int NormalDiameter = 200;
    private const int CriticalDiameter = 100;
    private const int ShrinkDurationSeconds = 10;
    private const int RestoreDurationSeconds = 5;
    private const int WarningDistance = 5;

    private BorderState _lastState = BorderState.Unknown;

    /// <summary>
    /// Initializes the world border center and default size on startup.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await rcon.SendCommandAsync("worldborder center 0 0", ct);
        await rcon.SendCommandAsync($"worldborder set {NormalDiameter}", ct);
        await rcon.SendCommandAsync("worldborder warning distance 0", ct);
        _lastState = BorderState.Normal;
        logger.LogInformation("World border initialized: center 0,0, diameter {Diameter}", NormalDiameter);
    }

    /// <summary>
    /// Evaluates fleet health and adjusts the world border accordingly, only on state transitions.
    /// </summary>
    public async Task UpdateWorldBorderAsync(CancellationToken ct = default)
    {
        if (monitor.TotalCount == 0) return;

        var healthyRatio = (double)monitor.HealthyCount / monitor.TotalCount;
        var desired = healthyRatio > 0.5 ? BorderState.Normal : BorderState.Critical;

        if (desired == _lastState) return;

        if (desired == BorderState.Critical)
        {
            await rcon.SendCommandAsync($"worldborder set {CriticalDiameter} {ShrinkDurationSeconds}", ct);
            await rcon.SendCommandAsync($"worldborder warning distance {WarningDistance}", ct);
            logger.LogWarning("World border CRITICAL: shrinking to {Diameter} over {Duration}s (healthy: {Healthy}/{Total})",
                CriticalDiameter, ShrinkDurationSeconds, monitor.HealthyCount, monitor.TotalCount);
        }
        else
        {
            await rcon.SendCommandAsync($"worldborder set {NormalDiameter} {RestoreDurationSeconds}", ct);
            await rcon.SendCommandAsync("worldborder warning distance 0", ct);
            logger.LogInformation("World border RESTORED: expanding to {Diameter} over {Duration}s (healthy: {Healthy}/{Total})",
                NormalDiameter, RestoreDurationSeconds, monitor.HealthyCount, monitor.TotalCount);
        }

        _lastState = desired;
    }

    private enum BorderState
    {
        Unknown,
        Normal,
        Critical
    }
}
