using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Builds a simplified canal system with one straight back canal and a side trunk to the lake.
/// - Back canal (E-W): runs along the BACK (north side) of all buildings
/// - Side trunk (N-S): runs on the EAST side of town, connecting back canal to lake
/// - Lake: positioned at the back of town, connected to trunk
/// Called by MinecraftWorldWorker after RCON is connected and resources are discovered.
/// </summary>
internal sealed class CanalService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<CanalService> logger)
{
    private bool _canalsBuilt;

    /// <summary>
    /// Tracks all canal (x, z) positions so MinecartRailService can detect bridges.
    /// </summary>
    public HashSet<(int x, int z)> CanalPositions { get; } = new();

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

        // Back canal Z position: a few blocks north of the northernmost building
        var backCanalZ = maxZ + 5;
        
        // Side trunk X position: east of all structures
        var trunkX = maxX + VillageLayout.CanalTotalWidth + 2;

        // Build the straight back canal (E-W) along the north side of all buildings
        await BuildBackCanalAsync(minX, backCanalZ, trunkX, ct);

        // Build the side trunk canal (N-S) from back canal down to lake
        await BuildTrunkCanalAsync(trunkX, backCanalZ, lakeZ, ct);

        // Open junction where back canal meets side trunk
        await OpenJunctionAsync(trunkX, backCanalZ, isEastWest: true, ct);

        // Build the lake
        await BuildLakeAsync(resourceCount, ct);

        // Open junction where trunk meets lake
        await OpenLakeJunctionAsync(trunkX, lakeZ, ct);
    }

    /// <summary>
    /// Builds the straight back canal (E-W) running along the north side of all buildings.
    /// This canal spans from the village's west edge to the side trunk on the east.
    /// </summary>
    private async Task BuildBackCanalAsync(int startX, int z, int endX, CancellationToken ct)
    {
        var canalY = VillageLayout.CanalY;
        var depth = VillageLayout.CanalDepth;
        var waterWidth = VillageLayout.CanalWaterWidth;

        await BuildCanalSegmentEastWestAsync(startX, z, endX, canalY, depth, waterWidth, ct);

        logger.LogInformation("Back canal built from X={StartX} to X={EndX} at Z={Z}", startX, endX, z);
    }

    /// <summary>
    /// Builds a horizontal (E-W) canal segment with stone brick walls, blue_ice floor, and water.
    /// </summary>
    private async Task BuildCanalSegmentEastWestAsync(int x1, int z, int x2, int canalY, int depth,
        int waterWidth, CancellationToken ct)
    {
        var minX = Math.Min(x1, x2);
        var maxX = Math.Max(x1, x2);
        if (minX > maxX) return;

        var wallZMin = z - waterWidth / 2 - 1;
        var wallZMax = z + waterWidth / 2 + 1;
        var waterZMin = z - waterWidth / 2;
        var waterZMax = z + waterWidth / 2;

        // Excavate
        await rcon.SendCommandAsync(
            $"fill {minX} {canalY - depth + 1} {wallZMin} {maxX} {VillageLayout.SurfaceY} {wallZMax} minecraft:air",
            CommandPriority.Normal, ct);

        // Floor: blue_ice
        await rcon.SendCommandAsync(
            $"fill {minX} {canalY - 1} {wallZMin} {maxX} {canalY - 1} {wallZMax} minecraft:blue_ice",
            CommandPriority.Normal, ct);

        // Walls
        await rcon.SendCommandAsync(
            $"fill {minX} {canalY - 1} {wallZMin} {maxX} {VillageLayout.SurfaceY} {wallZMin} minecraft:stone_bricks",
            CommandPriority.Normal, ct);
        await rcon.SendCommandAsync(
            $"fill {minX} {canalY - 1} {wallZMax} {maxX} {VillageLayout.SurfaceY} {wallZMax} minecraft:stone_bricks",
            CommandPriority.Normal, ct);

        // Water
        await rcon.SendCommandAsync(
            $"fill {minX} {canalY} {waterZMin} {maxX} {canalY} {waterZMax} minecraft:water",
            CommandPriority.Normal, ct);

        // Track positions
        for (var x = minX; x <= maxX; x++)
            for (var zz = wallZMin; zz <= wallZMax; zz++)
                CanalPositions.Add((x, zz));
    }

    /// <summary>
    /// Builds a vertical (N-S) canal segment with stone brick walls, blue_ice floor, and water.
    /// </summary>
    private async Task BuildCanalSegmentNorthSouthAsync(int x, int z1, int z2, int canalY, int depth,
        int waterWidth, CancellationToken ct)
    {
        var minZ = Math.Min(z1, z2);
        var maxZ = Math.Max(z1, z2);
        if (minZ > maxZ) return;

        var wallXMin = x - waterWidth / 2 - 1;
        var wallXMax = x + waterWidth / 2 + 1;
        var waterXMin = x - waterWidth / 2;
        var waterXMax = x + waterWidth / 2;

        // Excavate
        await rcon.SendCommandAsync(
            $"fill {wallXMin} {canalY - depth + 1} {minZ} {wallXMax} {VillageLayout.SurfaceY} {maxZ} minecraft:air",
            CommandPriority.Normal, ct);

        // Floor
        await rcon.SendCommandAsync(
            $"fill {wallXMin} {canalY - 1} {minZ} {wallXMax} {canalY - 1} {maxZ} minecraft:blue_ice",
            CommandPriority.Normal, ct);

        // Walls
        await rcon.SendCommandAsync(
            $"fill {wallXMin} {canalY - 1} {minZ} {wallXMin} {VillageLayout.SurfaceY} {maxZ} minecraft:stone_bricks",
            CommandPriority.Normal, ct);
        await rcon.SendCommandAsync(
            $"fill {wallXMax} {canalY - 1} {minZ} {wallXMax} {VillageLayout.SurfaceY} {maxZ} minecraft:stone_bricks",
            CommandPriority.Normal, ct);

        // Water
        await rcon.SendCommandAsync(
            $"fill {waterXMin} {canalY} {minZ} {waterXMax} {canalY} {maxZ} minecraft:water",
            CommandPriority.Normal, ct);

        // Track positions
        for (var xx = wallXMin; xx <= wallXMax; xx++)
            for (var zz = minZ; zz <= maxZ; zz++)
                CanalPositions.Add((xx, zz));
    }

    /// <summary>
    /// Builds the side trunk canal (N-S) running from the back canal down to the lake.
    /// </summary>
    private async Task BuildTrunkCanalAsync(int trunkX, int backCanalZ, int lakeZ, CancellationToken ct)
    {
        var canalY = VillageLayout.CanalY;
        var depth = VillageLayout.CanalDepth;
        var waterWidth = VillageLayout.CanalWaterWidth;

        await BuildCanalSegmentNorthSouthAsync(trunkX, backCanalZ, lakeZ, canalY, depth, waterWidth, ct);

        logger.LogInformation("Side trunk canal built at X={X} from Z={BackZ} to Z={LakeZ}", 
            trunkX, backCanalZ, lakeZ);
    }

    /// <summary>
    /// Opens a junction where two canals meet (T or L intersection).
    /// Removes wall blocks at the intersection to allow water flow.
    /// </summary>
    private async Task OpenJunctionAsync(int trunkX, int canalZ, bool isEastWest, CancellationToken ct)
    {
        var waterWidth = VillageLayout.CanalWaterWidth;
        var canalY = VillageLayout.CanalY;
        var depth = VillageLayout.CanalDepth;

        if (isEastWest)
        {
            // Back canal (E-W) meets trunk (N-S) — open trunk's north and south walls at junction
            var wallZMin = canalZ - waterWidth / 2 - 1;
            var wallZMax = canalZ + waterWidth / 2 + 1;
            var waterZMin = canalZ - waterWidth / 2;
            var waterZMax = canalZ + waterWidth / 2;
            var trunkWestWall = trunkX - waterWidth / 2 - 1;
            var trunkEastWall = trunkX + waterWidth / 2 + 1;

            // Open west wall of trunk where back canal arrives
            await rcon.SendCommandAsync(
                $"fill {trunkWestWall} {canalY - depth + 1} {waterZMin} {trunkWestWall} {VillageLayout.SurfaceY} {waterZMax} minecraft:air",
                CommandPriority.Normal, ct);
            await rcon.SendCommandAsync(
                $"fill {trunkWestWall} {canalY - 1} {waterZMin} {trunkWestWall} {canalY - 1} {waterZMax} minecraft:blue_ice",
                CommandPriority.Normal, ct);
            await rcon.SendCommandAsync(
                $"fill {trunkWestWall} {canalY} {waterZMin} {trunkWestWall} {canalY} {waterZMax} minecraft:water",
                CommandPriority.Normal, ct);
        }

        logger.LogInformation("Canal junction opened at X={X}, Z={Z}", trunkX, canalZ);
    }

    /// <summary>
    /// Opens the junction where the side trunk canal meets the lake.
    /// Removes the lake's north wall where the trunk arrives.
    /// </summary>
    private async Task OpenLakeJunctionAsync(int trunkX, int lakeZ, CancellationToken ct)
    {
        var waterWidth = VillageLayout.CanalWaterWidth;
        var canalY = VillageLayout.CanalY;
        var depth = VillageLayout.CanalDepth;
        var lakeWidth = VillageLayout.LakeWidth;

        // Lake northwest corner
        var (lakeX, _, _) = VillageLayout.GetLakePosition(0); // resourceCount not used for X calculation
        var lakeCenterX = lakeX + lakeWidth / 2;

        // Trunk canal arrives at lake's north wall — open an entrance
        var waterXMin = trunkX - waterWidth / 2;
        var waterXMax = trunkX + waterWidth / 2;

        // Remove north wall section where trunk connects
        await rcon.SendCommandAsync(
            $"fill {waterXMin} {canalY - depth + 1} {lakeZ} {waterXMax} {VillageLayout.SurfaceY} {lakeZ} minecraft:air",
            CommandPriority.Normal, ct);

        // Restore floor continuity
        await rcon.SendCommandAsync(
            $"fill {waterXMin} {VillageLayout.SurfaceY - VillageLayout.LakeBlockDepth} {lakeZ} {waterXMax} {VillageLayout.SurfaceY - VillageLayout.LakeBlockDepth} {lakeZ} minecraft:stone_bricks",
            CommandPriority.Normal, ct);

        // Water connection
        await rcon.SendCommandAsync(
            $"fill {waterXMin} {canalY} {lakeZ} {waterXMax} {canalY} {lakeZ} minecraft:water",
            CommandPriority.Normal, ct);

        logger.LogInformation("Lake junction opened at X={X}, Z={Z}", trunkX, lakeZ);
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
}
