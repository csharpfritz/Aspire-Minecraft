using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Builds beacon-powered towers for each monitored Aspire resource.
/// 3x3 iron block base, beacon on top, stained glass above for color indication.
/// Beam color matches Aspire dashboard resource type colors when healthy.
/// </summary>
internal sealed class BeaconTowerService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<BeaconTowerService> logger)
{
    private const int BaseX = 10;
    private const int BaseY = -60;
    private const int BaseZ = 14; // Offset from village structures (7 footprint + gap) to avoid overlap
    private const int Spacing = 10;

    // Maps Aspire resource types to stained glass colors matching the Aspire dashboard palette
    private static readonly Dictionary<string, string> ResourceTypeGlassColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Project"] = "blue_stained_glass",
        ["Container"] = "purple_stained_glass",
        ["Executable"] = "cyan_stained_glass",
    };

    private const string DefaultHealthyGlass = "light_blue_stained_glass";
    private const string UnhealthyGlass = "red_stained_glass";
    private const string StartingGlass = "yellow_stained_glass";
    private const string FinishedGlass = "gray_stained_glass";

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

    internal static string GetGlassBlock(ResourceInfo info)
    {
        return info.Status switch
        {
            ResourceStatus.Unhealthy => UnhealthyGlass,
            ResourceStatus.Unknown => StartingGlass,
            ResourceStatus.Healthy => ResourceTypeGlassColors.GetValueOrDefault(info.Type, DefaultHealthyGlass),
            _ => DefaultHealthyGlass,
        };
    }

    private async Task BuildBeaconTowerAsync(ResourceInfo info, int index, CancellationToken ct)
    {
        var col = index % VillageLayout.Columns;
        var row = index / VillageLayout.Columns;
        var x = BaseX + (col * Spacing);
        var y = BaseY;
        var z = BaseZ + (row * Spacing);

        // 3x3 iron block base (single layer)
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z} {x + 2} {y} {z + 2} minecraft:iron_block", ct);

        // Beacon on top center of the iron base
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 1} {z + 1} minecraft:beacon", ct);

        // Stained glass above the beacon — color reflects resource type and health state
        var glassBlock = GetGlassBlock(info);

        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 2} {z + 1} minecraft:{glassBlock}", ct);

        logger.LogDebug("Beacon tower built: {ResourceName} ({Type}/{Status}) at ({X},{Y},{Z})",
            info.Name, info.Type, info.Status, x, y, z);
    }
}
