using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Builds an individual per-building canal system with a side trunk connecting to a massive lake.
/// - Per-building canals (E-W): Each building gets its own short canal running along its BACK (Z-max / north side)
/// - Side trunk (N-S): Single trunk canal on the WEST side collecting all per-building canals
/// - Lake: Massive town-width lake at the back (Z-max) of town where the trunk connects (creeper boat landing zone)
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

        // Build the massive lake first (needed for trunk endpoint calculation)
        await BuildLakeAsync(resourceCount, ct);

        // Build the main north-south trunk canal spanning all building canals to the lake
        var trunkStartZ = int.MaxValue;
        for (var i = 0; i < orderedNames.Count; i++)
        {
            var (_, _, oz) = VillageLayout.GetStructureOrigin(orderedNames[i], i);
            var backZ = oz + VillageLayout.StructureSize + 4; // Must match per-building canal offset
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
    /// Builds an individual canal for a specific building.
    /// The canal runs along the BACK (Z-max / north side) of the building,
    /// then turns east to connect to the side trunk canal.
    /// </summary>
    private async Task BuildPerBuildingCanalAsync(string resourceName, int index, int trunkX, CancellationToken ct)
    {
        var (ox, _, oz) = VillageLayout.GetStructureOrigin(resourceName, index);
        var canalY = VillageLayout.CanalY;
        var depth = VillageLayout.CanalDepth;
        var waterWidth = VillageLayout.CanalWaterWidth;
        var structureSize = VillageLayout.StructureSize;

        // Canal runs behind (north of) the building at Z-max side
        // +4 clearance so canal walls don't overlap building decorations/footprint
        var buildingBackZ = oz + structureSize + 4;
        
        // E-W segment: from building's east edge across to the trunk's east wall
        var canalStartX = ox + structureSize;
        var trunkEastWall = trunkX + waterWidth / 2 + 1;
        var canalEndX = trunkEastWall;

        // Build the horizontal (E-W) canal segment behind the building
        await BuildCanalSegmentEastWestAsync(canalStartX, buildingBackZ, canalEndX, canalY, depth, waterWidth, ct);

        // Open the junction where this building's canal meets the trunk
        await OpenPerBuildingJunctionAsync(trunkX, buildingBackZ, ct);

        logger.LogInformation("Per-building canal built for {ResourceName} at Z={Z}", resourceName, buildingBackZ);
    }

    /// <summary>
    /// Builds a horizontal (E-W) canal segment with stone brick walls, blue_ice floor, and water.
    /// Fill commands are clipped against protected building regions to avoid damaging structures.
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

        // Excavate (clipped around protected buildings)
        await SendProtectedFillAsync(minX, canalY - depth + 1, wallZMin, maxX, VillageLayout.SurfaceY, wallZMax,
            "minecraft:air", ct);

        // Floor: blue_ice
        await SendProtectedFillAsync(minX, canalY - 1, wallZMin, maxX, canalY - 1, wallZMax,
            "minecraft:blue_ice", ct);

        // Walls
        await SendProtectedFillAsync(minX, canalY - 1, wallZMin, maxX, VillageLayout.SurfaceY, wallZMin,
            "minecraft:stone_bricks", ct);
        await SendProtectedFillAsync(minX, canalY - 1, wallZMax, maxX, VillageLayout.SurfaceY, wallZMax,
            "minecraft:stone_bricks", ct);

        // Water
        await SendProtectedFillAsync(minX, canalY, waterZMin, maxX, canalY, waterZMax,
            "minecraft:water", ct);

        // Track intended canal positions (full range, not clipped —
        // the building itself acts as a "bridge" for rail detection)
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
    /// Extends 2 blocks into the lake (beyond the north wall) to ensure visual connection.
    /// </summary>
    private async Task BuildTrunkCanalAsync(int trunkX, int backCanalZ, int lakeZ, CancellationToken ct)
    {
        var canalY = VillageLayout.CanalY;
        var depth = VillageLayout.CanalDepth;
        var waterWidth = VillageLayout.CanalWaterWidth;

        // Extend trunk canal 2 blocks into the lake to ensure connection beyond the north wall
        await BuildCanalSegmentNorthSouthAsync(trunkX, backCanalZ, lakeZ + 2, canalY, depth, waterWidth, ct);

        logger.LogInformation("Side trunk canal built at X={X} from Z={BackZ} to Z={LakeZ}+2", 
            trunkX, backCanalZ, lakeZ);
    }

    /// <summary>
    /// Opens the junction where a per-building canal (E-W) meets the side trunk (N-S).
    /// Removes the trunk's east wall where the building canal arrives to allow water flow.
    /// </summary>
    private async Task OpenPerBuildingJunctionAsync(int trunkX, int buildingCanalZ, CancellationToken ct)
    {
        var waterWidth = VillageLayout.CanalWaterWidth;
        var canalY = VillageLayout.CanalY;
        var depth = VillageLayout.CanalDepth;

        // Building canal (E-W) arrives at the trunk's east wall
        var waterZMin = buildingCanalZ - waterWidth / 2;
        var waterZMax = buildingCanalZ + waterWidth / 2;
        var trunkEastWall = trunkX + waterWidth / 2 + 1;

        // Open east wall of trunk where building canal arrives
        await rcon.SendCommandAsync(
            $"fill {trunkEastWall} {canalY - depth + 1} {waterZMin} {trunkEastWall} {VillageLayout.SurfaceY} {waterZMax} minecraft:air",
            CommandPriority.Normal, ct);
        
        // Restore floor continuity
        await rcon.SendCommandAsync(
            $"fill {trunkEastWall} {canalY - 1} {waterZMin} {trunkEastWall} {canalY - 1} {waterZMax} minecraft:blue_ice",
            CommandPriority.Normal, ct);
        
        // Water connection
        await rcon.SendCommandAsync(
            $"fill {trunkEastWall} {canalY} {waterZMin} {trunkEastWall} {canalY} {waterZMax} minecraft:water",
            CommandPriority.Normal, ct);

        logger.LogInformation("Per-building junction opened at X={X}, Z={Z}", trunkX, buildingCanalZ);
    }

    /// <summary>
    /// Opens the junction where the side trunk canal meets the lake.
    /// Removes the lake's north wall where the trunk arrives and fills water at both
    /// canal depth and lake depth for a smooth transition between the shallow canal and deep lake.
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
