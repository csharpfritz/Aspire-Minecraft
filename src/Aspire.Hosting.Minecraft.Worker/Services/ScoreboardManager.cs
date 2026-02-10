using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Manages a Minecraft scoreboard sidebar showing Aspire resource metrics.
/// </summary>
internal sealed class ScoreboardManager(
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

    private readonly HashSet<string> _previousEntries = [];

    private async Task UpdateScoresAsync(CancellationToken ct)
    {
        var currentEntries = new HashSet<string>();
        var score = monitor.TotalCount + 2;

        // Total resources
        var resLine = $"§fResources: §a{monitor.TotalCount}";
        await SetScore(resLine, score--, ct);
        currentEntries.Add(resLine);

        var healthLine = $"§fHealthy: §a{monitor.HealthyCount}";
        await SetScore(healthLine, score--, ct);
        currentEntries.Add(healthLine);

        // Individual resource statuses
        foreach (var (_, info) in monitor.Resources)
        {
            var statusIcon = info.Status switch
            {
                ResourceStatus.Healthy => "§a✔",
                ResourceStatus.Unhealthy => "§c✘",
                _ => "§e⏳"
            };
            var line = $"{statusIcon} {info.Name}";
            await SetScore(line, score--, ct);
            currentEntries.Add(line);
        }

        // Remove stale entries from previous cycle
        foreach (var stale in _previousEntries.Except(currentEntries))
        {
            await rcon.SendCommandAsync(
                $"""scoreboard players reset "{stale}" {ObjectiveName}""", ct);
        }

        _previousEntries.Clear();
        _previousEntries.UnionWith(currentEntries);

        logger.LogDebug("Scoreboard updated with {Count} resources", monitor.TotalCount);
    }

    private async Task SetScore(string name, int value, CancellationToken ct)
    {
        await rcon.SendCommandAsync(
            $"""scoreboard players set "{name}" {ObjectiveName} {value}""", ct);
    }
}
