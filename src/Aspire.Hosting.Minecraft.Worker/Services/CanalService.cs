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

        // Determine trunk canal X — east of all structures
        var (_, _, maxX, _) = VillageLayout.GetVillageBounds(resourceCount);
        var trunkX = maxX + VillageLayout.CanalTotalWidth + 2;

        // Build branch canals from each building to the trunk
        for (var i = 0; i < resourceCount; i++)
        {
            var (entranceX, _, entranceZ) = VillageLayout.GetCanalEntrance(i);
            await BuildBranchCanalAsync(entranceX, entranceZ, trunkX, ct);
        }

        // Build trunk canal running north–south
        var (lakeX, _, lakeZ) = VillageLayout.GetLakePosition(resourceCount);
        var firstEntrance = VillageLayout.GetCanalEntrance(0);
        var lastEntrance = VillageLayout.GetCanalEntrance(resourceCount - 1);
        var trunkMinZ = Math.Min(firstEntrance.z, lastEntrance.z) - 1;
        var trunkMaxZ = lakeZ;

        await BuildTrunkCanalAsync(trunkX, trunkMinZ, trunkMaxZ, ct);

        // Build the lake
        await BuildLakeAsync(resourceCount, ct);
    }

    private async Task BuildBranchCanalAsync(int startX, int z, int trunkX, CancellationToken ct)
    {
        var canalY = VillageLayout.CanalY;
        var depth = VillageLayout.CanalDepth;
        var waterWidth = VillageLayout.CanalWaterWidth;

        // Branch runs west→east from startX to trunkX
        var minX = Math.Min(startX, trunkX);
        var maxX = Math.Max(startX, trunkX);

        // Canal cross-section centered on z: wall, 3 water, wall (total width 5)
        var wallZMin = z - waterWidth / 2 - 1;
        var wallZMax = z + waterWidth / 2 + 1;
        var waterZMin = z - waterWidth / 2;
        var waterZMax = z + waterWidth / 2;

        // Excavate the canal trench
        await rcon.SendCommandAsync(
            $"fill {minX} {canalY - depth + 1} {wallZMin} {maxX} {VillageLayout.SurfaceY} {wallZMax} minecraft:air",
            CommandPriority.Normal, ct);

        // Floor: blue_ice
        await rcon.SendCommandAsync(
            $"fill {minX} {canalY - 1} {wallZMin} {maxX} {canalY - 1} {wallZMax} minecraft:blue_ice",
            CommandPriority.Normal, ct);

        // Walls: stone_bricks on both sides (from floor to surface)
        await rcon.SendCommandAsync(
            $"fill {minX} {canalY - 1} {wallZMin} {maxX} {VillageLayout.SurfaceY} {wallZMin} minecraft:stone_bricks",
            CommandPriority.Normal, ct);
        await rcon.SendCommandAsync(
            $"fill {minX} {canalY - 1} {wallZMax} {maxX} {VillageLayout.SurfaceY} {wallZMax} minecraft:stone_bricks",
            CommandPriority.Normal, ct);

        // Water source blocks at canal Y level
        await rcon.SendCommandAsync(
            $"fill {minX} {canalY} {waterZMin} {maxX} {canalY} {waterZMax} minecraft:water",
            CommandPriority.Normal, ct);

        // Track canal positions for bridge detection
        for (var x = minX; x <= maxX; x++)
        {
            for (var zz = wallZMin; zz <= wallZMax; zz++)
            {
                CanalPositions.Add((x, zz));
            }
        }
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
