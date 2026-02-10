using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Creates and updates floating hologram dashboards in the Minecraft world
/// using DecentHolograms plugin commands via RCON.
/// </summary>
public sealed class HologramManager(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<HologramManager> logger)
{
    private const string DashboardHologramId = "aspire_dashboard";
    private const int SpawnX = 0;
    private const int SpawnY = -55; // Above superflat surface (Y=-61), eye level
    private const int SpawnZ = 5;
    private bool _created;

    /// <summary>
    /// Creates or updates the Aspire dashboard hologram near spawn.
    /// </summary>
    public async Task UpdateDashboardAsync(CancellationToken ct = default)
    {
        using var activity = MinecraftMetrics.ActivitySource.StartActivity("minecraft.world.update_holograms");

        try
        {
            if (!_created)
            {
                await CreateHologramAsync(ct);
                _created = true;
            }

            await UpdateHologramLinesAsync(ct);

            logger.LogInformation("Hologram updated: {HologramId} {LineCount} {ResourceCount}",
                DashboardHologramId, monitor.Resources.Count + 2, monitor.Resources.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update hologram");
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
        }
    }

    private int _lastLineCount;

    private async Task CreateHologramAsync(CancellationToken ct)
    {
        // Delete existing hologram if any
        await rcon.SendCommandAsync($"dh delete {DashboardHologramId}", ct);
        await Task.Delay(500, ct);

        // Create new hologram at spawn (RCON requires -l:world:x:y:z)
        await rcon.SendCommandAsync(
            $"dh create {DashboardHologramId} -l:world:{SpawnX}:{SpawnY}:{SpawnZ} &b&l=== Aspire Dashboard ===", ct);
        _lastLineCount = 1;
    }

    private async Task UpdateHologramLinesAsync(CancellationToken ct)
    {
        var lines = new List<string> { "&b&l=== Aspire Dashboard ===" };

        foreach (var (_, info) in monitor.Resources)
        {
            var (icon, color) = info.Status switch
            {
                ResourceStatus.Healthy => ("✔", "&a"),
                ResourceStatus.Unhealthy => ("✘", "&c"),
                _ => ("⏳", "&e")
            };
            lines.Add($"{color}{icon} {info.Name} ({info.Status})");
        }

        lines.Add($"&f{monitor.HealthyCount}/{monitor.TotalCount} services healthy");

        // Ensure we have enough lines (add if needed)
        while (_lastLineCount < lines.Count)
        {
            _lastLineCount++;
            await rcon.SendCommandAsync($"dh line add {DashboardHologramId} 1 &7...", ct);
        }

        // Remove excess lines (shrink if resources were removed)
        while (_lastLineCount > lines.Count)
        {
            await rcon.SendCommandAsync($"dh line remove {DashboardHologramId} 1 {_lastLineCount}", ct);
            _lastLineCount--;
        }

        // Update all lines
        for (var i = 0; i < lines.Count; i++)
        {
            await rcon.SendCommandAsync($"dh line set {DashboardHologramId} 1 {i + 1} {lines[i]}", ct);
        }
    }
}
