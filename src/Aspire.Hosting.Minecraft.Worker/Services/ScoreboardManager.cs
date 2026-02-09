using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Manages a Minecraft scoreboard sidebar showing Aspire resource metrics.
/// </summary>
public sealed class ScoreboardManager(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<ScoreboardManager> logger)
{
    private const string ObjectiveName = "aspire";
    private bool _initialized;

    /// <summary>
    /// Creates or updates the Aspire scoreboard sidebar.
    /// </summary>
    public async Task UpdateScoreboardAsync(CancellationToken ct = default)
    {
        using var activity = MinecraftMetrics.ActivitySource.StartActivity("minecraft.world.update_scoreboard");

        try
        {
            if (!_initialized)
            {
                await InitializeScoreboardAsync(ct);
                _initialized = true;
            }

            await UpdateScoresAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update scoreboard");
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
        }
    }

    private async Task InitializeScoreboardAsync(CancellationToken ct)
    {
        // Remove existing objective if any
        await rcon.SendCommandAsync($"scoreboard objectives remove {ObjectiveName}", ct);
        await Task.Delay(200, ct);

        // Create the objective and set display
        await rcon.SendCommandAsync(
            $"scoreboard objectives add {ObjectiveName} dummy \"§b§lAspire Status\"", ct);
        await rcon.SendCommandAsync($"scoreboard objectives setdisplay sidebar {ObjectiveName}", ct);
    }

    private async Task UpdateScoresAsync(CancellationToken ct)
    {
        var score = monitor.TotalCount + 2;

        // Total resources
        await SetScore($"§fResources: §a{monitor.TotalCount}", score--, ct);
        await SetScore($"§fHealthy: §a{monitor.HealthyCount}", score--, ct);

        // Individual resource statuses
        foreach (var (_, info) in monitor.Resources)
        {
            var statusIcon = info.Status switch
            {
                ResourceStatus.Healthy => "§a✔",
                ResourceStatus.Unhealthy => "§c✘",
                _ => "§e⏳"
            };
            await SetScore($"{statusIcon} {info.Name}", score--, ct);
        }

        logger.LogDebug("Scoreboard updated with {Count} resources", monitor.TotalCount);
    }

    private async Task SetScore(string name, int value, CancellationToken ct)
    {
        // Use fake player names for scoreboard entries
        await rcon.SendCommandAsync(
            $"""scoreboard players set "{name}" {ObjectiveName} {value}""", ct);
    }
}
