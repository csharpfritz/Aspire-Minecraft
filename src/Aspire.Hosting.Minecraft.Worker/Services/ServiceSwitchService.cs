using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Places Minecraft levers and redstone lamps on each resource structure to visually represent
/// service status. When a resource is healthy the lever is ON and the lamp is lit; when unhealthy
/// the lever flips OFF and the lamp goes dark. This is visual only — levers reflect state,
/// they do not control Aspire resources.
/// Called by MinecraftWorldWorker after RCON is connected and resources are discovered.
/// </summary>
internal sealed class ServiceSwitchService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<ServiceSwitchService> logger)
{
    private bool _switchesPlaced;
    private readonly Dictionary<string, ResourceStatus> _lastKnownStatus = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Updates switch states. Places switches on first call, then tracks health transitions.
    /// Called each worker cycle.
    /// </summary>
    public async Task UpdateAsync(CancellationToken ct = default)
    {
        try
        {
            if (!_switchesPlaced)
            {
                await PlaceAllSwitchesAsync(ct);
                _switchesPlaced = true;
            }

            await UpdateSwitchStatesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating service switches");
        }
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

            // Place lever on the front wall (z-min side) at x+2, y+2, z, facing outward (north)
            await PlaceLeverAsync(x + 2, y + 2, z, powered, ct);

            // Place redstone lamp above the lever at x+2, y+3, z
            await PlaceLampAsync(x + 2, y + 3, z, powered, ct);

            _lastKnownStatus[name] = info.Status;

            logger.LogInformation("Service switch placed for {ResourceName} at ({X},{Y},{Z}), powered={Powered}",
                name, x + 2, y + 2, z, powered);
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

            var (x, y, z) = VillageLayout.GetStructureOrigin(i);
            var powered = info.Status == ResourceStatus.Healthy;

            // Always place switches (self-healing if destroyed)
            await PlaceLeverAsync(x + 2, y + 2, z, powered, ct);
            await PlaceLampAsync(x + 2, y + 3, z, powered, ct);

            _lastKnownStatus.TryGetValue(name, out var lastStatus);
            if (info.Status != lastStatus)
            {
                _lastKnownStatus[name] = info.Status;
                logger.LogInformation("Service switch updated for {ResourceName}: powered={Powered}",
                    name, powered);
            }
        }
    }

    private async Task PlaceLeverAsync(int x, int y, int z, bool powered, CancellationToken ct)
    {
        var poweredState = powered ? "true" : "false";
        await rcon.SendCommandAsync(
            $"setblock {x} {y} {z} minecraft:lever[face=wall,facing=north,powered={poweredState}]",
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
