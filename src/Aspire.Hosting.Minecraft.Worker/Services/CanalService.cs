using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Builds water canals from each building's east side to a shared lake at the village end.
/// Branch canals run eastward from each building, merge into a north–south trunk canal,
/// which feeds into a decorative lake with a small dock.
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

        // Collect all building footprints for collision avoidance
        var footprints = VillageLayout.GetAllBuildingFootprints(orderedNames);

        // Determine trunk canal X — east of all structures
        var (_, _, maxX, _) = VillageLayout.GetVillageBounds(resourceCount);
        var trunkX = maxX + VillageLayout.CanalTotalWidth + 2;

        // Build branch canals from each building to the trunk, routing around other buildings
        for (var i = 0; i < resourceCount; i++)
        {
            var (entranceX, _, entranceZ) = VillageLayout.GetCanalEntrance(orderedNames[i], i);
            await BuildBranchCanalAsync(entranceX, entranceZ, trunkX, i, footprints, ct);
        }

        // Build trunk canal running north–south
        var (lakeX, _, lakeZ) = VillageLayout.GetLakePosition(resourceCount);

        // Find Z-range from all canal entrances
        var minEntrZ = int.MaxValue;
        var maxEntrZ = int.MinValue;
        for (var i = 0; i < resourceCount; i++)
        {
            var (_, _, ez) = VillageLayout.GetCanalEntrance(orderedNames[i], i);
            minEntrZ = Math.Min(minEntrZ, ez);
            maxEntrZ = Math.Max(maxEntrZ, ez);
        }
        var trunkMinZ = minEntrZ - 1;
        var trunkMaxZ = lakeZ;

        await BuildTrunkCanalAsync(trunkX, trunkMinZ, trunkMaxZ, ct);

        // Build the lake
        await BuildLakeAsync(resourceCount, ct);
    }

    private async Task BuildBranchCanalAsync(int startX, int z, int trunkX, int buildingIndex,
        IReadOnlyList<(int minX, int minZ, int maxX, int maxZ)> footprints, CancellationToken ct)
    {
        var canalY = VillageLayout.CanalY;
        var depth = VillageLayout.CanalDepth;
        var waterWidth = VillageLayout.CanalWaterWidth;

        // Build the branch in segments, routing around any buildings in the path.
        // Strategy: try straight-line first. If blocked, detour south (z + offset) around the building.
        var segments = CalculateBranchSegments(startX, z, trunkX, buildingIndex, waterWidth, footprints);

        foreach (var seg in segments)
        {
            await BuildCanalSegmentAsync(seg.x1, seg.z, seg.x2, canalY, depth, waterWidth, ct);
        }

        // Connect vertical (Z-direction) segments where detours occurred
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var cur = segments[i];
            var next = segments[i + 1];
            if (cur.z != next.z)
            {
                // Build a short N-S connector between the two Z levels
                var connX = cur.x2; // connect at the east end of current / west end of next
                var minZ = Math.Min(cur.z, next.z);
                var maxZ = Math.Max(cur.z, next.z);
                await BuildCanalConnectorAsync(connX, minZ, maxZ, canalY, depth, waterWidth, ct);
            }
        }
    }

    /// <summary>
    /// Calculate horizontal canal segments from startX to trunkX at initial Z,
    /// detouring around any building footprints encountered.
    /// Each segment is (x1, z, x2) — a horizontal run at fixed Z.
    /// </summary>
    private static List<(int x1, int z, int x2)> CalculateBranchSegments(
        int startX, int z, int trunkX, int buildingIndex, int waterWidth,
        IReadOnlyList<(int minX, int minZ, int maxX, int maxZ)> footprints)
    {
        var segments = new List<(int x1, int z, int x2)>();
        var currentX = startX;
        var currentZ = z;

        while (currentX < trunkX)
        {
            // Check if straight-line from currentX to trunkX at currentZ hits any building
            var hitBuilding = FindFirstBlockingBuilding(currentX, trunkX, currentZ, waterWidth,
                buildingIndex, footprints);

            if (hitBuilding == null)
            {
                // Clear path to trunk — add final segment
                segments.Add((currentX, currentZ, trunkX));
                break;
            }

            var (bMinX, bMinZ, bMaxX, bMaxZ) = hitBuilding.Value;

            // Add segment up to the blocking building (stop 2 blocks before)
            if (currentX < bMinX - 2)
            {
                segments.Add((currentX, currentZ, bMinX - 2));
                currentX = bMinX - 2;
            }

            // Detour: route south of the building (below its maxZ)
            var detourZ = bMaxZ + waterWidth / 2 + 3;
            segments.Add((currentX, detourZ, bMaxX + 2));
            currentX = bMaxX + 2;
            currentZ = detourZ;
        }

        return segments;
    }

    /// <summary>
    /// Find the first building footprint that blocks a horizontal canal segment.
    /// </summary>
    private static (int minX, int minZ, int maxX, int maxZ)? FindFirstBlockingBuilding(
        int fromX, int toX, int z, int waterWidth, int excludeIndex,
        IReadOnlyList<(int minX, int minZ, int maxX, int maxZ)> footprints)
    {
        var segZMin = z - waterWidth / 2 - 1;
        var segZMax = z + waterWidth / 2 + 1;
        (int minX, int minZ, int maxX, int maxZ)? closest = null;

        for (var i = 0; i < footprints.Count; i++)
        {
            if (i == excludeIndex) continue;
            var fp = footprints[i];
            // Check if this building's footprint overlaps the canal segment
            if (fp.maxX >= fromX && fp.minX <= toX && fp.maxZ >= segZMin && fp.minZ <= segZMax)
            {
                if (closest == null || fp.minX < closest.Value.minX)
                    closest = fp;
            }
        }
        return closest;
    }

    private async Task BuildCanalSegmentAsync(int x1, int z, int x2, int canalY, int depth,
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

    private async Task BuildCanalConnectorAsync(int x, int minZ, int maxZ, int canalY, int depth,
        int waterWidth, CancellationToken ct)
    {
        // Short N-S canal segment connecting two branch Z-levels
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

        // Place walkway bridge over the connector (oak planks at surface + 1)
        var bridgeY = VillageLayout.SurfaceY;
        await rcon.SendCommandAsync(
            $"fill {wallXMin} {bridgeY} {minZ} {wallXMax} {bridgeY} {maxZ} minecraft:oak_planks",
            CommandPriority.Normal, ct);
    }

    private async Task BuildTrunkCanalAsync(int trunkX, int minZ, int maxZ, CancellationToken ct)
    {
        var canalY = VillageLayout.CanalY;
        var waterWidth = VillageLayout.CanalWaterWidth;

        // Trunk canal cross-section centered on trunkX
        var wallXMin = trunkX - waterWidth / 2 - 1;
        var wallXMax = trunkX + waterWidth / 2 + 1;
        var waterXMin = trunkX - waterWidth / 2;
        var waterXMax = trunkX + waterWidth / 2;

        // Excavate
        await rcon.SendCommandAsync(
            $"fill {wallXMin} {canalY - VillageLayout.CanalDepth + 1} {minZ} {wallXMax} {VillageLayout.SurfaceY} {maxZ} minecraft:air",
            CommandPriority.Normal, ct);

        // Floor: blue_ice
        await rcon.SendCommandAsync(
            $"fill {wallXMin} {canalY - 1} {minZ} {wallXMax} {canalY - 1} {maxZ} minecraft:blue_ice",
            CommandPriority.Normal, ct);

        // Walls: stone_bricks on both sides
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
        for (var x = wallXMin; x <= wallXMax; x++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                CanalPositions.Add((x, z));
            }
        }
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
