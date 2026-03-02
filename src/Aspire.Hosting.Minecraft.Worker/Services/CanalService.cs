using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Builds a rail network with per-building segments and a side trunk connecting to a massive lake.
/// - Per-building rails (E-W): Each building gets its own rail segment running along its BACK (Z-max / north side)
/// - Side trunk (N-S): Single trunk rail on the WEST side collecting all per-building rails
/// - Lake: Massive town-width lake at the back (Z-max) of town (creeper minecart landing zone)
/// Rails are placed directly on the ground surface (SurfaceY + 1). Powered rails every ~8 blocks
/// with redstone torches for activation. Regular rails at junctions allow autonomous curve turning.
/// Called by MinecraftWorldWorker after RCON is connected and resources are discovered.
/// </summary>
internal sealed class CanalService(
    RconService rcon,
    AspireResourceMonitor monitor,
    BuildingProtectionService protection,
    ILogger<CanalService> logger)
{
    private bool _canalsBuilt;

    /// <summary>
    /// Tracks all canal (x, z) positions so MinecartRailService can detect bridges.
    /// </summary>
    public HashSet<(int x, int z)> CanalPositions { get; } = new();

    /// <summary>
    /// Z coordinates where per-building E-W rails meet the N-S trunk.
    /// Used by PlaceNorthSouthRailsAsync to place regular rails (instead of powered) at junctions.
    /// </summary>
    private readonly HashSet<int> _junctionZPositions = new();

    /// <summary>
    /// X coordinate of the trunk canal center, set during initialization.
    /// Used by ErrorBoatService to detect when minecarts reach the trunk.
    /// </summary>
    public int TrunkCanalX { get; private set; }

    /// <summary>
    /// Spacing (in blocks) between powered rails for sustained minecart speed.
    /// </summary>
    private const int PoweredRailInterval = 8;

    /// <summary>
    /// One-time initialization: builds canals and lake. Called after DiscoverResources.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_canalsBuilt) return;

        logger.LogInformation("Canal service initializing...");
        using var burst = rcon.EnterBurstMode(40);
        await BuildCanalNetworkAsync(ct);
        _canalsBuilt = true;
        logger.LogInformation("Canal service initialized");
    }

    /// <summary>
    /// Updates canal state each worker cycle. Currently a no-op.
    /// </summary>
    public Task UpdateAsync(CancellationToken ct = default) => Task.CompletedTask;

    private async Task BuildCanalNetworkAsync(CancellationToken ct)
    {
        var resources = monitor.Resources;
        var orderedNames = VillageLayout.ReorderByDependency(resources);
        var resourceCount = orderedNames.Count;

        if (resourceCount == 0) return;

        var (minX, _, maxX, maxZ) = VillageLayout.GetVillageBounds(resourceCount);
        var (lakeX, _, lakeZ) = VillageLayout.GetLakePosition(resourceCount);

        // Side trunk X position: west of all structures
        var trunkX = minX - VillageLayout.CanalTotalWidth - 2;
        TrunkCanalX = trunkX;

        // Build the massive lake first (needed for trunk endpoint calculation)
        await BuildLakeAsync(resourceCount, ct);

        // Build the main north-south trunk canal spanning all building canals to the lake
        var trunkStartZ = int.MaxValue;
        for (var i = 0; i < orderedNames.Count; i++)
        {
            var (_, _, oz) = VillageLayout.GetStructureOrigin(orderedNames[i], i);
            var backZ = oz + VillageLayout.StructureSize + 4; // Must match per-building canal offset
            _junctionZPositions.Add(backZ);
            trunkStartZ = Math.Min(trunkStartZ, backZ);
        }
        trunkStartZ -= 4; // Gap south of the first building canal
        await BuildTrunkCanalAsync(trunkX, trunkStartZ, lakeZ, ct);

        // Build individual per-building canals that connect to the trunk
        for (var i = 0; i < orderedNames.Count; i++)
        {
            await BuildPerBuildingCanalAsync(orderedNames[i], i, trunkX, ct);
        }

        // Open junction where trunk meets lake
        await OpenLakeJunctionAsync(trunkX, lakeZ, ct);
    }

    /// <summary>
    /// Builds an individual rail segment for a specific building.
    /// The segment runs along the BACK (Z-max / north side) of the building,
    /// heading west to connect to the side trunk rail.
    /// </summary>
    private async Task BuildPerBuildingCanalAsync(string resourceName, int index, int trunkX, CancellationToken ct)
    {
        var (ox, _, oz) = VillageLayout.GetStructureOrigin(resourceName, index);
        var waterWidth = VillageLayout.CanalWaterWidth;
        var structureSize = VillageLayout.StructureSize;

        // Rail runs behind (north of) the building at Z-max side
        // +4 clearance so rails don't overlap building decorations/footprint
        var buildingBackZ = oz + structureSize + 4;
        
        // E-W segment: from building's east edge to one block east of the trunk junction corner
        var canalStartX = ox + structureSize;
        var canalEndX = trunkX + 1;

        // Build the horizontal (E-W) rail segment behind the building
        await BuildCanalSegmentEastWestAsync(canalStartX, buildingBackZ, canalEndX, waterWidth, ct);

        // Open the junction where this building's rail meets the trunk
        await OpenPerBuildingJunctionAsync(trunkX, buildingBackZ, ct);

        logger.LogInformation("Rail segment built for {ResourceName} at Z={Z}", resourceName, buildingBackZ);
    }

    /// <summary>
    /// Builds a horizontal (E-W) rail segment with powered rails on the ground surface.
    /// Powered rails propel minecarts westward to the trunk. The westernmost rail is a
    /// regular rail to enable auto-curving at the trunk junction.
    /// </summary>
    private async Task BuildCanalSegmentEastWestAsync(int x1, int z, int x2,
        int waterWidth, CancellationToken ct)
    {
        var minX = Math.Min(x1, x2);
        var maxX = Math.Max(x1, x2);
        if (minX > maxX) return;

        var wallZMin = z - waterWidth / 2 - 1;
        var wallZMax = z + waterWidth / 2 + 1;

        // Place rails on top of the ground surface (no canal excavation needed for minecarts)
        await PlaceEastWestRailsAsync(minX, maxX, VillageLayout.SurfaceY + 1, z, ct);

        // Track intended positions (full range —
        // MinecartRailService uses this for bridge detection)
        for (var x = minX; x <= maxX; x++)
            for (var zz = wallZMin; zz <= wallZMax; zz++)
                CanalPositions.Add((x, zz));
    }

    /// <summary>
    /// Builds a vertical (N-S) rail segment with powered rails on the ground surface.
    /// Powered rails propel minecarts southward toward the lake. At junction Z positions,
    /// regular rails are used to allow auto-curving at per-building junctions.
    /// </summary>
    private async Task BuildCanalSegmentNorthSouthAsync(int x, int z1, int z2,
        int waterWidth, CancellationToken ct)
    {
        var minZ = Math.Min(z1, z2);
        var maxZ = Math.Max(z1, z2);
        if (minZ > maxZ) return;

        var wallXMin = x - waterWidth / 2 - 1;
        var wallXMax = x + waterWidth / 2 + 1;

        // Place rails on top of the ground surface (no canal excavation needed for minecarts)
        await PlaceNorthSouthRailsAsync(minZ, maxZ, VillageLayout.SurfaceY + 1, x, ct);

        // Track positions
        for (var xx = wallXMin; xx <= wallXMax; xx++)
            for (var zz = minZ; zz <= maxZ; zz++)
                CanalPositions.Add((xx, zz));
    }

    /// <summary>
    /// Builds the side trunk rail (N-S) running from the back rail down to the lake.
    /// Extends 2 blocks into the lake (beyond the north wall) to ensure visual connection.
    /// </summary>
    private async Task BuildTrunkCanalAsync(int trunkX, int backCanalZ, int lakeZ, CancellationToken ct)
    {
        var waterWidth = VillageLayout.CanalWaterWidth;

        // Extend trunk rail 2 blocks into the lake to ensure connection beyond the north wall
        await BuildCanalSegmentNorthSouthAsync(trunkX, backCanalZ, lakeZ + 2, waterWidth, ct);

        logger.LogInformation("Trunk rail built at X={X} from Z={BackZ} to Z={LakeZ}+2", 
            trunkX, backCanalZ, lakeZ);
    }

    /// <summary>
    /// Opens the junction where a per-building rail (E-W) meets the side trunk (N-S).
    /// Places a regular rail at the corner so minecarts auto-curve from westbound to southbound.
    /// The regular rail detects perpendicular neighbors and forms an L-turn automatically.
    /// </summary>
    private async Task OpenPerBuildingJunctionAsync(int trunkX, int buildingCanalZ, CancellationToken ct)
    {
        var railY = VillageLayout.SurfaceY + 1;

        // Place a regular rail at the corner where E-W meets N-S.
        // With perpendicular neighbors (east from E-W segment, south from N-S trunk),
        // Minecraft auto-curves this rail for the westbound-to-southbound turn.
        await rcon.SendCommandAsync(
            $"setblock {trunkX} {railY} {buildingCanalZ} minecraft:rail",
            CommandPriority.Normal, ct);

        // Redstone torch near junction to power adjacent powered rails
        await rcon.SendCommandAsync(
            $"setblock {trunkX + 1} {railY} {buildingCanalZ + 2} minecraft:redstone_torch",
            CommandPriority.Normal, ct);

        logger.LogInformation("Rail junction with curve at X={X}, Z={Z}", trunkX, buildingCanalZ);
    }

    /// <summary>
    /// Opens the junction where the side trunk canal meets the lake.
    /// Removes the lake's north wall where the trunk arrives and fills water at both
    /// canal depth and lake depth for a smooth ice-to-water transition where minecarts enter the lake.
    /// </summary>
    private async Task OpenLakeJunctionAsync(int trunkX, int lakeZ, CancellationToken ct)
    {
        var waterWidth = VillageLayout.CanalWaterWidth;
        var canalY = VillageLayout.CanalY;
        var lakeDepth = VillageLayout.LakeBlockDepth;

        // Trunk canal arrives at lake's north wall — open an entrance
        var waterXMin = trunkX - waterWidth / 2;
        var waterXMax = trunkX + waterWidth / 2;

        // Open 2 blocks deep (lakeZ and lakeZ+1) so the wall is fully removed
        // and the canal water merges cleanly with lake interior water.
        var junctionZEnd = lakeZ + 1;

        // Remove north wall section where trunk connects (2 Z blocks)
        await rcon.SendCommandAsync(
            $"fill {waterXMin} {VillageLayout.SurfaceY - lakeDepth + 1} {lakeZ} {waterXMax} {VillageLayout.SurfaceY} {junctionZEnd} minecraft:air",
            CommandPriority.Normal, ct);

        // Restore floor continuity at lake depth
        await rcon.SendCommandAsync(
            $"fill {waterXMin} {VillageLayout.SurfaceY - lakeDepth} {lakeZ} {waterXMax} {VillageLayout.SurfaceY - lakeDepth} {junctionZEnd} minecraft:stone_bricks",
            CommandPriority.Normal, ct);

        // Water connection at both canal depth (SurfaceY-1) and lake depth (SurfaceY-2)
        // so there's no air gap between the shallow trunk canal and the deeper lake.
        await rcon.SendCommandAsync(
            $"fill {waterXMin} {VillageLayout.SurfaceY - lakeDepth + 1} {lakeZ} {waterXMax} {canalY} {junctionZEnd} minecraft:water",
            CommandPriority.Normal, ct);

        logger.LogInformation("Lake junction opened at X={X}, Z={Z} to Z={ZEnd}", trunkX, lakeZ, junctionZEnd);
    }

    private async Task BuildLakeAsync(int resourceCount, CancellationToken ct)
    {
        var (lakeX, lakeY, lakeZ) = VillageLayout.GetLakePosition(resourceCount);
        var lakeWidth = VillageLayout.LakeWidth;
        var lakeLength = VillageLayout.LakeLength;

        var x1 = lakeX;
        var z1 = lakeZ;
        var x2 = lakeX + lakeWidth - 1;
        var z2 = lakeZ + lakeLength - 1;

        // Excavate lake area
        await rcon.SendCommandAsync(
            $"fill {x1} {lakeY} {z1} {x2} {VillageLayout.SurfaceY} {z2} minecraft:air",
            CommandPriority.Normal, ct);

        // Stone bricks floor
        await rcon.SendCommandAsync(
            $"fill {x1} {lakeY} {z1} {x2} {lakeY} {z2} minecraft:stone_bricks",
            CommandPriority.Normal, ct);

        // Stone bricks perimeter walls (4 sides, from floor to surface)
        await rcon.SendCommandAsync(
            $"fill {x1} {lakeY} {z1} {x1} {VillageLayout.SurfaceY} {z2} minecraft:stone_bricks",
            CommandPriority.Normal, ct);
        await rcon.SendCommandAsync(
            $"fill {x2} {lakeY} {z1} {x2} {VillageLayout.SurfaceY} {z2} minecraft:stone_bricks",
            CommandPriority.Normal, ct);
        await rcon.SendCommandAsync(
            $"fill {x1} {lakeY} {z1} {x2} {VillageLayout.SurfaceY} {z1} minecraft:stone_bricks",
            CommandPriority.Normal, ct);
        await rcon.SendCommandAsync(
            $"fill {x1} {lakeY} {z2} {x2} {VillageLayout.SurfaceY} {z2} minecraft:stone_bricks",
            CommandPriority.Normal, ct);

        // Fill interior with water (inside walls, above floor)
        await rcon.SendCommandAsync(
            $"fill {x1 + 1} {lakeY + 1} {z1 + 1} {x2 - 1} {VillageLayout.SurfaceY - 1} {z2 - 1} minecraft:water",
            CommandPriority.Normal, ct);

        // Small dock on the north side (center of north edge)
        var dockCenterX = (x1 + x2) / 2;
        var dockY = VillageLayout.SurfaceY;
        var dockZ = z1;

        // Dock platform: oak_planks (5 wide, 3 deep, at surface level)
        await rcon.SendCommandAsync(
            $"fill {dockCenterX - 2} {dockY} {dockZ - 2} {dockCenterX + 2} {dockY} {dockZ} minecraft:oak_planks",
            CommandPriority.Normal, ct);

        // Fence posts around dock edges
        for (var dx = -2; dx <= 2; dx++)
        {
            await rcon.SendCommandAsync(
                $"setblock {dockCenterX + dx} {dockY + 1} {dockZ - 2} minecraft:oak_fence",
                CommandPriority.Normal, ct);
        }
        await rcon.SendCommandAsync(
            $"setblock {dockCenterX - 2} {dockY + 1} {dockZ - 1} minecraft:oak_fence",
            CommandPriority.Normal, ct);
        await rcon.SendCommandAsync(
            $"setblock {dockCenterX + 2} {dockY + 1} {dockZ - 1} minecraft:oak_fence",
            CommandPriority.Normal, ct);

        logger.LogInformation("Lake built at ({X}, {Z}) with dock", lakeX, lakeZ);
    }

    /// <summary>
    /// Places rails along an E-W segment at the given Y and center Z.
    /// Powered rails every <see cref="PoweredRailInterval"/> blocks, with redstone torches for activation.
    /// The westernmost rail (at minX) is a regular rail to enable auto-curving at the trunk junction.
    /// </summary>
    private async Task PlaceEastWestRailsAsync(int minX, int maxX, int canalY, int centerZ, CancellationToken ct)
    {
        for (var x = minX; x <= maxX; x++)
        {
            // Last rail (westernmost, approaching junction) is regular rail for auto-curving at the corner
            var block = x == minX
                ? "minecraft:rail[shape=east_west]"
                : "minecraft:powered_rail[shape=east_west]";

            await rcon.SendCommandAsync(
                $"setblock {x} {canalY} {centerZ} {block}",
                CommandPriority.Normal, ct);

            // Redstone torch every PoweredRailInterval blocks to activate powered rails (skip regular rail at minX)
            if (x != minX && (x - minX) % PoweredRailInterval == 0)
            {
                await rcon.SendCommandAsync(
                    $"setblock {x} {canalY} {centerZ + 2} minecraft:redstone_torch",
                    CommandPriority.Normal, ct);
            }
        }
    }

    /// <summary>
    /// Places rails along a N-S segment at the given Y and center X.
    /// Powered rails every <see cref="PoweredRailInterval"/> blocks, with redstone torches for activation.
    /// At junction Z positions, regular rails are used instead of powered rails so the adjacent
    /// junction corner rail can auto-curve.
    /// </summary>
    private async Task PlaceNorthSouthRailsAsync(int minZ, int maxZ, int canalY, int centerX, CancellationToken ct)
    {
        for (var z = minZ; z <= maxZ; z++)
        {
            // At junction Z positions, use regular rail so the corner rail can auto-curve
            var block = _junctionZPositions.Contains(z)
                ? "minecraft:rail[shape=north_south]"
                : "minecraft:powered_rail[shape=north_south]";

            await rcon.SendCommandAsync(
                $"setblock {centerX} {canalY} {z} {block}",
                CommandPriority.Normal, ct);

            if ((z - minZ) % PoweredRailInterval == 0)
            {
                await rcon.SendCommandAsync(
                    $"setblock {centerX - 2} {canalY} {z} minecraft:redstone_torch",
                    CommandPriority.Normal, ct);
            }
        }
    }

    /// <summary>
    /// Sends a fill command that is clipped against all registered building protection zones.
    /// If the fill region overlaps any building, it is split into sub-regions that avoid the building.
    /// </summary>
    private async Task SendProtectedFillAsync(
        int minX, int minY, int minZ, int maxX, int maxY, int maxZ,
        string block, CancellationToken ct)
    {
        var subRegions = protection.ClipFill(minX, minY, minZ, maxX, maxY, maxZ);
        foreach (var (fMinX, fMinY, fMinZ, fMaxX, fMaxY, fMaxZ) in subRegions)
        {
            await rcon.SendCommandAsync(
                $"fill {fMinX} {fMinY} {fMinZ} {fMaxX} {fMaxY} {fMaxZ} {block}",
                CommandPriority.Normal, ct);
        }
    }
}
