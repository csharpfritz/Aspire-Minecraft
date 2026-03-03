using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Builds a rail network with per-building E-W segments, per-row N-S lines, and a massive lake.
/// - Per-building rails (E-W): Each building gets a rail segment running along its BACK (Z-max side)
/// - Per-row N-S lines: Each row of buildings gets its own independent N-S rail line heading to the lake.
///   Northernmost rows get the furthest-west lines so E-W branches never cross other N-S lines.
/// - Curve junctions: Explicit south_east curves at the E-W/N-S intersection turn westbound minecarts south
/// - Lake: Massive town-width lake at the southern end (creeper minecart landing zone)
/// Rails are placed directly on the ground surface (SurfaceY + 1). Powered rails every ~8 blocks
/// with redstone torches for activation. Called by MinecraftWorldWorker after RCON is connected.
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
    /// X coordinate of the base trunk position, set during initialization.
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

        // Base N-S line X position: west of all structures
        var baseTrunkX = minX - VillageLayout.CanalTotalWidth - 2;
        TrunkCanalX = baseTrunkX;

        // Build the massive lake first
        await BuildLakeAsync(resourceCount, ct);

        // Assign each unique backZ (row of buildings) a dedicated N-S line X position.
        // Northernmost rows get the FURTHEST WEST lines so that E-W branches from
        // southern rows never cross northern rows' N-S lines.
        var uniqueBackZsSorted = new SortedSet<int>();
        for (var i = 0; i < orderedNames.Count; i++)
        {
            var (_, _, oz) = VillageLayout.GetStructureOrigin(orderedNames[i], i);
            uniqueBackZsSorted.Add(oz + VillageLayout.StructureSize + 4);
        }
        var sortedBackZs = uniqueBackZsSorted.ToList(); // ascending: north → south
        var lineAssignments = new Dictionary<int, int>(); // backZ → lineX
        for (var j = 0; j < sortedBackZs.Count; j++)
        {
            // Northernmost (j=0) gets furthest west, southernmost gets closest to buildings
            lineAssignments[sortedBackZs[j]] = baseTrunkX - ((sortedBackZs.Count - 1 - j) * 2);
        }

        // Phase 1: Place all E-W branch rails
        for (var i = 0; i < orderedNames.Count; i++)
        {
            var (ox, _, oz) = VillageLayout.GetStructureOrigin(orderedNames[i], i);
            var backZ = oz + VillageLayout.StructureSize + 4;
            var lineX = lineAssignments[backZ];
            var canalStartX = ox + VillageLayout.StructureSize;
            await BuildCanalSegmentEastWestAsync(canalStartX, backZ, lineX + 1,
                VillageLayout.CanalWaterWidth, ct);
        }

        // Phase 2: Place all N-S powered rails (once per unique line)
        var builtNSLines = new HashSet<int>();
        foreach (var (backZ, lineX) in lineAssignments)
        {
            if (builtNSLines.Add(lineX))
            {
                await PlaceNorthSouthLineAsync(lineX, backZ + 1, lakeZ + 2, ct);
            }
        }

        // Phase 3: Place all curve rails LAST — explicit south_east shape turns
        // westbound minecarts southward. Placed after all neighbors to prevent
        // Minecraft from auto-recalculating the curve shape.
        var placedCurves = new HashSet<(int, int)>();
        for (var i = 0; i < orderedNames.Count; i++)
        {
            var (_, _, oz) = VillageLayout.GetStructureOrigin(orderedNames[i], i);
            var backZ = oz + VillageLayout.StructureSize + 4;
            var lineX = lineAssignments[backZ];
            if (placedCurves.Add((lineX, backZ)))
            {
                var railY = VillageLayout.SurfaceY + 1;
                await rcon.SendCommandAsync(
                    $"setblock {lineX} {railY} {backZ} minecraft:rail[shape=south_east]",
                    CommandPriority.Normal, ct);
                CanalPositions.Add((lineX, backZ));

                logger.LogInformation("Rail curve (south_east) at X={X}, Z={Z}", lineX, backZ);
            }
        }

        // Open lake junctions for each N-S line
        foreach (var lineX in lineAssignments.Values.Distinct())
        {
            await OpenLakeJunctionAsync(lineX, lakeZ, ct);
        }
    }

    /// <summary>
    /// Builds a horizontal (E-W) rail segmentwith powered rails on the ground surface.
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
                    $"setblock {x} {canalY} {centerZ + 1} minecraft:redstone_torch",
                    CommandPriority.Normal, ct);
            }
        }
    }

    /// <summary>
    /// Places powered N-S rails along an independent line from startZ to endZ.
    /// Each line is dedicated to one row of buildings — no shared trunk, no junction logic needed.
    /// Redstone torches every <see cref="PoweredRailInterval"/> blocks for sustained speed.
    /// </summary>
    private async Task PlaceNorthSouthLineAsync(int lineX, int startZ, int endZ, CancellationToken ct)
    {
        var railY = VillageLayout.SurfaceY + 1;

        for (var z = startZ; z <= endZ; z++)
        {
            await rcon.SendCommandAsync(
                $"setblock {lineX} {railY} {z} minecraft:powered_rail[shape=north_south]",
                CommandPriority.Normal, ct);

            if ((z - startZ) % PoweredRailInterval == 0)
            {
                await rcon.SendCommandAsync(
                    $"setblock {lineX - 1} {railY} {z} minecraft:redstone_torch",
                    CommandPriority.Normal, ct);
            }
        }

        // Track positions for bridge detection by MinecartRailService
        for (var z = startZ; z <= endZ; z++)
            CanalPositions.Add((lineX, z));
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
