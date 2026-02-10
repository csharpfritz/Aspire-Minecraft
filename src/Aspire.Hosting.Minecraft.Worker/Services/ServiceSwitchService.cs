using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Places Minecraft levers and redstone lamps on each resource structure to visually represent
/// service status. When a resource is healthy the lever is ON and the lamp is lit; when unhealthy
/// the lever flips OFF and the lamp goes dark. This is visual only — levers reflect state,
/// they do not control Aspire resources.
/// </summary>
internal sealed class ServiceSwitchService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<ServiceSwitchService> logger) : BackgroundService
{
    private bool _switchesPlaced;
    private readonly Dictionary<string, ResourceStatus> _lastKnownStatus = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Service switch service starting, waiting for resources...");

        // Wait until resources are discovered and structures are built
        while (monitor.TotalCount == 0 && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }

        // Extra delay to let structures finish building
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        logger.LogInformation("Service switch service active");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_switchesPlaced)
                {
                    await PlaceAllSwitchesAsync(stoppingToken);
                    _switchesPlaced = true;
                }

                await UpdateSwitchStatesAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in service switch loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("Service switch service stopping");
    }

    private async Task PlaceAllSwitchesAsync(CancellationToken ct)
    {
        var orderedNames = VillageLayout.ReorderByDependency(monitor.Resources);

        for (var i = 0; i < orderedNames.Count; i++)
        {
            var name = orderedNames[i];
            if (!monitor.Resources.TryGetValue(name, out var info)) continue;

            var (x, y, z) = VillageLayout.GetStructureOrigin(i);
            var powered = info.Status == ResourceStatus.Healthy;

            // Place lever on the front wall (z-min side) at x+1, y+2
            await PlaceLeverAsync(x + 1, y + 2, z, powered, ct);

            // Place redstone lamp behind the lever (inside the wall) at x+1, y+3, z
            await PlaceLampAsync(x + 1, y + 3, z, powered, ct);

            _lastKnownStatus[name] = info.Status;

            logger.LogInformation("Service switch placed for {ResourceName} at ({X},{Y},{Z}), powered={Powered}",
                name, x + 1, y + 2, z, powered);
        }

        logger.LogInformation("Service switches placed for {Count} resources", orderedNames.Count);
    }

    private async Task UpdateSwitchStatesAsync(CancellationToken ct)
    {
        var orderedNames = VillageLayout.ReorderByDependency(monitor.Resources);

        for (var i = 0; i < orderedNames.Count; i++)
        {
            var name = orderedNames[i];
            if (!monitor.Resources.TryGetValue(name, out var info)) continue;

            _lastKnownStatus.TryGetValue(name, out var lastStatus);
            if (info.Status == lastStatus) continue;

            _lastKnownStatus[name] = info.Status;

            var (x, y, z) = VillageLayout.GetStructureOrigin(i);
            var powered = info.Status == ResourceStatus.Healthy;

            await PlaceLeverAsync(x + 1, y + 2, z, powered, ct);
            await PlaceLampAsync(x + 1, y + 3, z, powered, ct);

            logger.LogInformation("Service switch updated for {ResourceName}: powered={Powered}",
                name, powered);
        }
    }

    private async Task PlaceLeverAsync(int x, int y, int z, bool powered, CancellationToken ct)
    {
        var poweredState = powered ? "true" : "false";
        await rcon.SendCommandAsync(
            $"setblock {x} {y} {z} minecraft:lever[face=wall,facing=south,powered={poweredState}]",
            CommandPriority.Normal, ct);
    }

    private async Task PlaceLampAsync(int x, int y, int z, bool powered, CancellationToken ct)
    {
        // Use lit glowstone when powered (always visible), unlit redstone lamp when not
        var block = powered ? "minecraft:glowstone" : "minecraft:redstone_lamp";
        await rcon.SendCommandAsync(
            $"setblock {x} {y} {z} {block}",
            CommandPriority.Normal, ct);
    }
}
