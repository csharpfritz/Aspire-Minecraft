using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Builds beacon-powered towers for each monitored Aspire resource.
/// 3x3 iron block base, beacon on top, stained glass above for color indication.
/// Green glass = healthy, red glass = unhealthy.
/// </summary>
internal sealed class BeaconTowerService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<BeaconTowerService> logger)
{
    private const int BaseX = 10;
    private const int BaseY = -60;
    private const int BaseZ = 8; // Offset from main structures to avoid overlap
    private const int Spacing = 6;

    /// <summary>
    /// Builds or updates beacon towers for all monitored resources.
    /// </summary>
    public async Task UpdateBeaconTowersAsync(CancellationToken ct = default)
    {
        var index = 0;
        foreach (var (_, info) in monitor.Resources)
        {
            await BuildBeaconTowerAsync(info, index, ct);
            index++;
        }

        logger.LogDebug("Beacon towers updated for {Count} resources", monitor.TotalCount);
    }

    private async Task BuildBeaconTowerAsync(ResourceInfo info, int index, CancellationToken ct)
    {
        var x = BaseX + (index * Spacing);
        var y = BaseY;
        var z = BaseZ;

        // 3x3 iron block base (single layer)
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z} {x + 2} {y} {z + 2} minecraft:iron_block", ct);

        // Beacon on top center of the iron base
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 1} {z + 1} minecraft:beacon", ct);

        // Stained glass above the beacon for color indication
        var glassBlock = info.Status == ResourceStatus.Unhealthy
            ? "minecraft:red_stained_glass"
            : "minecraft:green_stained_glass";

        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 2} {z + 1} {glassBlock}", ct);

        logger.LogDebug("Beacon tower built: {ResourceName} ({Status}) at ({X},{Y},{Z})",
            info.Name, info.Status, x, y, z);
    }
}
