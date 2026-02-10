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
    private bool _created;
    private int _lastValue = -1;
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
                $$"""bossbar add {{BossBarId}} {"text":"Aspire Fleet Health","color":"green","bold":true}""", ct);
            await rcon.SendCommandAsync($"bossbar set {BossBarId} max 100", ct);
            await rcon.SendCommandAsync($"bossbar set {BossBarId} players @a", ct);
            await rcon.SendCommandAsync($"bossbar set {BossBarId} visible true", ct);
            _created = true;
        }

        var value = (int)((double)monitor.HealthyCount / monitor.TotalCount * 100);
        var color = value switch
        {
            100 => "green",
            >= 50 => "yellow",
            _ => "red"
        };

        if (value != _lastValue)
        {
            await rcon.SendCommandAsync($"bossbar set {BossBarId} value {value}", ct);
            await rcon.SendCommandAsync(
                $$"""bossbar set {{BossBarId}} name {"text":"Aspire Fleet Health: {{value}}%","bold":true}""", ct);
            _lastValue = value;
        }

        if (color != _lastColor)
        {
            await rcon.SendCommandAsync($"bossbar set {BossBarId} color {color}", ct);
            _lastColor = color;
        }

        logger.LogDebug("Boss bar updated: {Value}% ({Color})", value, color);
    }
}
