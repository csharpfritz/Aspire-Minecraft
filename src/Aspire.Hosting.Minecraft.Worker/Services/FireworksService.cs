using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Launches fireworks when all monitored resources recover to healthy.
/// Tracks whether any resource was previously unhealthy, and triggers only on full recovery.
/// </summary>
internal sealed class FireworksService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<FireworksService> logger)
{
    private bool _wasAnyUnhealthy;

    /// <summary>
    /// Checks fleet health after changes and launches fireworks on all-green recovery.
    /// </summary>
    public async Task CheckAndLaunchFireworksAsync(IReadOnlyList<ResourceStatusChange> changes, CancellationToken ct = default)
    {
        // Track if any resource has been unhealthy
        if (monitor.Resources.Values.Any(r => r.Status == ResourceStatus.Unhealthy))
        {
            _wasAnyUnhealthy = true;
        }

        // Only fire if we previously had an unhealthy resource and now all are healthy
        if (_wasAnyUnhealthy
            && monitor.TotalCount > 0
            && monitor.HealthyCount == monitor.TotalCount
            && changes.Any(c => c.NewStatus == ResourceStatus.Healthy))
        {
            await LaunchFireworksAsync(ct);
            _wasAnyUnhealthy = false;
        }
    }

    private async Task LaunchFireworksAsync(CancellationToken ct)
    {
        // Launch fireworks at several positions around the resource area
        var positions = new (int x, int y, int z)[]
        {
            (10, -58, 0),
            (16, -58, 0),
            (22, -58, 0),
            (10, -58, 4),
            (16, -58, 4),
        };

        foreach (var (x, y, z) in positions)
        {
            await rcon.SendCommandAsync(
                $"summon minecraft:firework_rocket {x} {y} {z}", ct);
        }

        logger.LogInformation("Fireworks launched for all-green recovery at {Count} positions", positions.Length);
    }
}
