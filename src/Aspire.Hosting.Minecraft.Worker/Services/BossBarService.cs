using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Manages a persistent boss bar showing overall Aspire fleet health percentage.
/// Color: green = all healthy, yellow = degraded, red = majority down.
/// </summary>
internal sealed class BossBarService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<BossBarService> logger)
{
    private const string BossBarId = "aspire:fleet_health";
    private readonly string _appName = Environment.GetEnvironmentVariable("ASPIRE_APP_NAME") ?? "Aspire";
    private bool _created;
    private int _lastValue = -1;
    private bool _nameSet;
    private string _lastColor = "";

    /// <summary>
    /// Creates (if needed) and updates the fleet health boss bar.
    /// </summary>
    public async Task UpdateBossBarAsync(CancellationToken ct = default)
    {
        if (monitor.TotalCount == 0) return;

        if (!_created)
        {
            await rcon.SendCommandAsync(
                $"bossbar add {BossBarId} \"{_appName} Fleet Health\"", ct);
            await rcon.SendCommandAsync($"bossbar set {BossBarId} max 100", ct);
            await rcon.SendCommandAsync($"bossbar set {BossBarId} visible true", ct);
            _created = true;
        }

        // Re-send players @a every cycle so newly joined players see the boss bar
        await rcon.SendCommandAsync($"bossbar set {BossBarId} players @a", ct);

        var value = (int)((double)monitor.HealthyCount / monitor.TotalCount * 100);
        var color = value switch
        {
            100 => "green",
            >= 50 => "yellow",
            _ => "red"
        };

        if (value != _lastValue || !_nameSet)
        {
            await rcon.SendCommandAsync($"bossbar set {BossBarId} value {value}", ct);
            await rcon.SendCommandAsync(
                $"bossbar set {BossBarId} name \"{_appName} Fleet Health: {value} percent\"", ct);
            _lastValue = value;
            _nameSet = true;
        }

        if (color != _lastColor)
        {
            await rcon.SendCommandAsync($"bossbar set {BossBarId} color {color}", ct);
            _lastColor = color;
        }

        logger.LogDebug("Boss bar updated: {Value}% ({Color})", value, color);
    }
}
