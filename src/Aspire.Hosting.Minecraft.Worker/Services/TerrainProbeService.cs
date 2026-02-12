using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Detects the terrain surface Y coordinate at startup using RCON binary search.
/// Uses <c>setblock X Y Z yellow_wool keep</c> to probe for solid blocks:
/// success means air (placed wool, then cleaned up), failure means solid block found.
/// Binary search from Y=100 down to Y=-64, requiring ~8 RCON commands.
/// Sets <see cref="VillageLayout.SurfaceY"/> for all subsequent services.
/// </summary>
internal sealed class TerrainProbeService(
    RconService rcon,
    ILogger<TerrainProbeService> logger)
{
    private const int MaxY = 100;
    private const int MinY = -64;
    private const string ProbeBlock = "minecraft:yellow_wool";

    /// <summary>
    /// Detects the surface Y coordinate at (BaseX, BaseZ) using binary search with RCON setblock.
    /// On success, sets <see cref="VillageLayout.SurfaceY"/>. On failure, leaves the default (<see cref="VillageLayout.BaseY"/>).
    /// </summary>
    public async Task DetectSurfaceAsync(CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Terrain probe starting at ({X}, {Z})...", VillageLayout.BaseX, VillageLayout.BaseZ);

            var surfaceY = await BinarySearchSurfaceAsync(VillageLayout.BaseX, VillageLayout.BaseZ, ct);

            if (surfaceY.HasValue)
            {
                VillageLayout.SurfaceY = surfaceY.Value;
                logger.LogInformation("Terrain probe detected surface Y={SurfaceY}", surfaceY.Value);
            }
            else
            {
                logger.LogWarning("Terrain probe could not detect surface — using default Y={BaseY}", VillageLayout.BaseY);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Terrain probe failed — using default Y={BaseY}", VillageLayout.BaseY);
        }
    }

    /// <summary>
    /// Binary search for the highest solid block at (x, z).
    /// Returns the Y of the highest solid block, or null if detection fails.
    /// </summary>
    internal async Task<int?> BinarySearchSurfaceAsync(int x, int z, CancellationToken ct)
    {
        var low = MinY;
        var high = MaxY;

        // First, verify probe works at a known-air location (high up)
        var testResult = await ProbeBlockAsync(x, high, z, ct);
        if (testResult == null)
        {
            // RCON not responding properly
            return null;
        }

        // If the block at MaxY is solid, surface is very high — return MaxY
        if (testResult == false)
        {
            return high;
        }

        while (low < high)
        {
            var mid = low + (high - low + 1) / 2;

            var isAir = await ProbeBlockAsync(x, mid, z, ct);
            if (isAir == null)
            {
                return null; // RCON error, abort
            }

            if (isAir.Value)
            {
                // Air at mid — surface is below mid
                high = mid - 1;
            }
            else
            {
                // Solid at mid — surface is at or above mid
                low = mid;
            }
        }

        return low;
    }

    /// <summary>
    /// Probes a single block position. Returns true if air (wool was placed and cleaned up),
    /// false if solid (placement failed), or null if RCON response was unparseable.
    /// </summary>
    internal async Task<bool?> ProbeBlockAsync(int x, int y, int z, CancellationToken ct)
    {
        try
        {
            var response = await rcon.SendCommandAsync(
                $"setblock {x} {y} {z} {ProbeBlock} keep", ct);

            if (string.IsNullOrEmpty(response))
                return null;

            // "Changed the block at X, Y, Z" → air was there, we placed wool → clean up
            if (response.Contains("Changed the block", StringComparison.OrdinalIgnoreCase))
            {
                // Clean up: remove the probe block
                await rcon.SendCommandAsync(
                    $"setblock {x} {y} {z} minecraft:air", ct);
                return true;
            }

            // "That position is not loaded in the world" → can't probe, treat as error
            if (response.Contains("not loaded", StringComparison.OrdinalIgnoreCase))
                return null;

            // Any other response (e.g., "Could not set the block") → solid block there
            return false;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Probe failed at ({X}, {Y}, {Z})", x, y, z);
            return null;
        }
    }
}
