using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Builds a standalone 21×21, 32-block-tall Grand Observation Tower north of the
/// Aspire village. Five themed floors connected by a continuous counter-clockwise
/// spiral staircase (oak stairs). Players walk from ground to roof without jumping.
///
/// Placement is computed dynamically via <see cref="SetPosition"/>: the tower is
/// centered on the village X-axis and placed <see cref="NorthGap"/> blocks north
/// of the fence perimeter's north edge. Entrance faces south (toward the village).
/// Built once at startup.
/// </summary>
internal sealed class GrandObservationTowerService(
    RconService rcon,
    BuildingProtectionService protection,
    ILogger<GrandObservationTowerService> logger)
{
    private bool _built;

    // Tower footprint and height (fixed).
    private const int TowerSize = 21;       // 21×21 footprint
    private const int TowerHeight = 32;     // y+1 through y+32

    /// <summary>Gap in blocks between the fence's north edge and the tower's south wall.</summary>
    internal const int NorthGap = 15;

    // Dynamic tower origin, computed from the village layout via SetPosition.
    private int _towerOriginX;
    private int _towerOriginZ;
    private int _fenceMinZ;
    private int _villageCenterX;
    private bool _positionSet;

    private int TowerMaxX => _towerOriginX + TowerSize - 1;
    private int TowerMaxZ => _towerOriginZ + TowerSize - 1;

    /// <summary>
    /// Computes and stores the tower's origin from the village layout.
    /// Must be called before <see cref="ForceloadAsync"/> and <see cref="BuildTowerAsync"/>.
    /// </summary>
    /// <param name="resourceCount">Number of discovered Aspire resources (determines village width).</param>
    public void SetPosition(int resourceCount)
    {
        var (fMinX, fMinZ, fMaxX, _) = VillageLayout.GetFencePerimeter(resourceCount);
        _villageCenterX = (fMinX + fMaxX) / 2;
        _fenceMinZ = fMinZ;
        _towerOriginX = _villageCenterX - TowerSize / 2;
        _towerOriginZ = fMinZ - NorthGap - TowerSize;
        _positionSet = true;
        logger.LogInformation("Tower position calculated: ({X},{Z}) for {Count} resources",
            _towerOriginX, _towerOriginZ, resourceCount);
    }

    /// <summary>
    /// Forceloads the tower chunk area and the walkway gap to the fence.
    /// Must be called before <see cref="BuildTowerAsync"/>.
    /// </summary>
    public async Task ForceloadAsync(CancellationToken ct)
    {
        EnsurePositionSet();
        // Extend forceload south to cover the walkway between tower and fence
        var forceloadMaxZ = Math.Max(TowerMaxZ + 2, _fenceMinZ);
        await rcon.SendCommandAsync(
            $"forceload add {_towerOriginX - 2} {_towerOriginZ - 2} {TowerMaxX + 2} {forceloadMaxZ}", ct);
        logger.LogInformation("Forceloaded tower+walkway area: ({X1},{Z1}) to ({X2},{Z2})",
            _towerOriginX - 2, _towerOriginZ - 2, TowerMaxX + 2, forceloadMaxZ);
    }

    /// <summary>
    /// Registers the tower's 3D bounding box with the building protection service
    /// so canals, rails, and other subsystems avoid the tower volume.
    /// </summary>
    public void RegisterProtection()
    {
        EnsurePositionSet();
        var y = VillageLayout.SurfaceY;
        protection.Register(
            _towerOriginX - 1, y, _towerOriginZ - 2,
            TowerMaxX + 1, y + TowerHeight, TowerMaxZ + 1,
            "GrandObservationTower");
        logger.LogInformation("Tower protection zone registered");
    }

    /// <summary>
    /// Builds the entire Grand Observation Tower: exterior shell, 5 themed floors,
    /// continuous spiral staircase, and all interior decorations.
    /// </summary>
    public async Task BuildTowerAsync(CancellationToken ct)
    {
        if (_built) return;
        EnsurePositionSet();

        logger.LogInformation("Building Grand Observation Tower at ({X},{Z})...", _towerOriginX, _towerOriginZ);
        using var burst = rcon.EnterBurstMode(40);

        var y = VillageLayout.SurfaceY;
        var x1 = _towerOriginX;
        var z1 = _towerOriginZ;
        var x2 = TowerMaxX;
        var z2 = TowerMaxZ;

        // ============================
        // PHASE 1: EXTERIOR SHELL
        // ============================
        await BuildExteriorAsync(x1, y, z1, x2, z2, ct);

        // ============================
        // PHASE 2: FLOOR PLATFORMS
        // ============================
        await BuildFloorPlatformsAsync(x1, y, z1, x2, z2, ct);

        // ============================
        // PHASE 3: SPIRAL STAIRCASE
        // ============================
        await BuildSpiralStaircaseAsync(x1, y, z1, x2, z2, ct);

        // ============================
        // PHASE 4: FLOOR INTERIORS
        // ============================
        await BuildFloor1EntranceHallAsync(x1, y, z1, x2, z2, ct);
        await BuildFloor2LibraryAsync(x1, y, z1, x2, z2, ct);
        await BuildFloor3ArmoryAsync(x1, y, z1, x2, z2, ct);
        await BuildFloor4ObservatoryAsync(x1, y, z1, x2, z2, ct);
        await BuildFloor5RooftopAsync(x1, y, z1, x2, z2, ct);

        // ============================
        // PHASE 5: WALKWAY TO VILLAGE
        // ============================
        await BuildWalkwayAsync(y, ct);

        _built = true;
        logger.LogInformation("Grand Observation Tower complete — 5 floors, spiral staircase, walkway, full decorations");
    }

    private void EnsurePositionSet()
    {
        if (!_positionSet)
            throw new InvalidOperationException(
                "Tower position not set. Call SetPosition(resourceCount) before building.");
    }

    // =========================================================================
    // EXTERIOR
    // =========================================================================

    private async Task BuildExteriorAsync(int x1, int y, int z1, int x2, int z2, CancellationToken ct)
    {
        // Base plinth: mossy stone bricks (weathered foundation)
        await rcon.SendCommandAsync(
            $"fill {x1} {y} {z1} {x2} {y} {z2} minecraft:mossy_stone_bricks", ct);

        // Main walls: stone brick hollow shell (y+1 to y+31)
        await rcon.SendCommandAsync(
            $"fill {x1} {y + 1} {z1} {x2} {y + 31} {z2} minecraft:stone_bricks hollow", ct);

        // Clear the interior floor at y+1 so ground floor is at ground level (y)
        // The 'hollow' fill creates a stone_bricks bottom face at y+1; removing it
        // makes the mossy stone brick plinth at y the walkable floor — flush with the walkway.
        await rcon.SendCommandAsync(
            $"fill {x1 + 1} {y + 1} {z1 + 1} {x2 - 1} {y + 1} {z2 - 1} minecraft:air", ct);

        // Corner buttresses: deepslate brick pillars (3×3, rise to y+33)
        await rcon.SendCommandAsync(
            $"fill {x1} {y + 1} {z1} {x1 + 2} {y + 33} {z1 + 2} minecraft:deepslate_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x2 - 2} {y + 1} {z1} {x2} {y + 33} {z1 + 2} minecraft:deepslate_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x1} {y + 1} {z2 - 2} {x1 + 2} {y + 33} {z2} minecraft:deepslate_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x2 - 2} {y + 1} {z2 - 2} {x2} {y + 33} {z2} minecraft:deepslate_bricks", ct);

        // Weathering: cracked stone on lower walls (front and back)
        await rcon.SendCommandAsync(
            $"fill {x1 + 3} {y + 1} {z1} {x2 - 3} {y + 2} {z1} minecraft:cracked_stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x1 + 3} {y + 1} {z2} {x2 - 3} {y + 2} {z2} minecraft:cracked_stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x1} {y + 1} {z1 + 3} {x1} {y + 2} {z2 - 3} minecraft:cracked_stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x2} {y + 1} {z1 + 3} {x2} {y + 2} {z2 - 3} minecraft:cracked_stone_bricks", ct);

        // String courses: stone brick stairs at y+7 and y+17 (floor transition bands)
        await rcon.SendCommandAsync(
            $"fill {x1 + 3} {y + 7} {z1} {x2 - 3} {y + 7} {z1} minecraft:stone_brick_stairs[facing=south,half=top]", ct);
        await rcon.SendCommandAsync(
            $"fill {x1 + 3} {y + 7} {z2} {x2 - 3} {y + 7} {z2} minecraft:stone_brick_stairs[facing=north,half=top]", ct);
        await rcon.SendCommandAsync(
            $"fill {x1 + 3} {y + 17} {z1} {x2 - 3} {y + 17} {z1} minecraft:stone_brick_stairs[facing=south,half=top]", ct);
        await rcon.SendCommandAsync(
            $"fill {x1 + 3} {y + 17} {z2} {x2 - 3} {y + 17} {z2} minecraft:stone_brick_stairs[facing=north,half=top]", ct);

        // Arrow slits: iron bars at floors 1–2 (lower levels)
        await rcon.SendCommandAsync(
            $"fill {x1 + 5} {y + 3} {z1} {x1 + 6} {y + 4} {z1} minecraft:iron_bars", ct);
        await rcon.SendCommandAsync(
            $"fill {x2 - 6} {y + 3} {z1} {x2 - 5} {y + 4} {z1} minecraft:iron_bars", ct);
        await rcon.SendCommandAsync(
            $"fill {x1 + 5} {y + 3} {z2} {x1 + 6} {y + 4} {z2} minecraft:iron_bars", ct);
        await rcon.SendCommandAsync(
            $"fill {x2 - 6} {y + 3} {z2} {x2 - 5} {y + 4} {z2} minecraft:iron_bars", ct);

        // Observation windows: large glass panes on all 4 sides at floor 4 (y+19 to y+21)
        await rcon.SendCommandAsync(
            $"fill {x1 + 5} {y + 19} {z1} {x2 - 5} {y + 21} {z1} minecraft:glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x1 + 5} {y + 19} {z2} {x2 - 5} {y + 21} {z2} minecraft:glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x1} {y + 19} {z1 + 5} {x1} {y + 21} {z2 - 5} minecraft:glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x2} {y + 19} {z1 + 5} {x2} {y + 21} {z2 - 5} minecraft:glass_pane", ct);

        // Machicolations: upside-down stairs below parapet
        await rcon.SendCommandAsync(
            $"fill {x1 + 3} {y + 30} {z1} {x2 - 3} {y + 30} {z1} minecraft:stone_brick_stairs[facing=south,half=top]", ct);
        await rcon.SendCommandAsync(
            $"fill {x1 + 3} {y + 30} {z2} {x2 - 3} {y + 30} {z2} minecraft:stone_brick_stairs[facing=north,half=top]", ct);
        await rcon.SendCommandAsync(
            $"fill {x1} {y + 30} {z1 + 3} {x1} {y + 30} {z2 - 3} minecraft:stone_brick_stairs[facing=east,half=top]", ct);
        await rcon.SendCommandAsync(
            $"fill {x2} {y + 30} {z1 + 3} {x2} {y + 30} {z2 - 3} minecraft:stone_brick_stairs[facing=west,half=top]", ct);

        // Parapet ring at y+31
        await rcon.SendCommandAsync(
            $"fill {x1} {y + 31} {z1} {x2} {y + 31} {z2} minecraft:stone_bricks hollow", ct);
        // Clear parapet interior (walkable roof)
        await rcon.SendCommandAsync(
            $"fill {x1 + 1} {y + 31} {z1 + 1} {x2 - 1} {y + 31} {z2 - 1} minecraft:air", ct);

        // Battlements / merlons: 2-wide raised blocks at cardinal directions
        await rcon.SendCommandAsync(
            $"fill {x1 + 5} {y + 32} {z1} {x1 + 6} {y + 32} {z1} minecraft:stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x2 - 6} {y + 32} {z1} {x2 - 5} {y + 32} {z1} minecraft:stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x1 + 9} {y + 32} {z1} {x1 + 11} {y + 32} {z1} minecraft:stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x1 + 5} {y + 32} {z2} {x1 + 6} {y + 32} {z2} minecraft:stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x2 - 6} {y + 32} {z2} {x2 - 5} {y + 32} {z2} minecraft:stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x1 + 9} {y + 32} {z2} {x1 + 11} {y + 32} {z2} minecraft:stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x1} {y + 32} {z1 + 5} {x1} {y + 32} {z1 + 6} minecraft:stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x1} {y + 32} {z2 - 6} {x1} {y + 32} {z2 - 5} minecraft:stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x2} {y + 32} {z1 + 5} {x2} {y + 32} {z1 + 6} minecraft:stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x2} {y + 32} {z2 - 6} {x2} {y + 32} {z2 - 5} minecraft:stone_bricks", ct);

        // Corner turret caps: stone brick stairs on top of buttresses
        await rcon.SendCommandAsync(
            $"fill {x1} {y + 34} {z1} {x1 + 2} {y + 34} {z1 + 2} minecraft:stone_brick_stairs[facing=south,half=top]", ct);
        await rcon.SendCommandAsync(
            $"fill {x2 - 2} {y + 34} {z1} {x2} {y + 34} {z1 + 2} minecraft:stone_brick_stairs[facing=south,half=top]", ct);
        await rcon.SendCommandAsync(
            $"fill {x1} {y + 34} {z2 - 2} {x1 + 2} {y + 34} {z2} minecraft:stone_brick_stairs[facing=north,half=top]", ct);
        await rcon.SendCommandAsync(
            $"fill {x2 - 2} {y + 34} {z2 - 2} {x2} {y + 34} {z2} minecraft:stone_brick_stairs[facing=north,half=top]", ct);

        // Entrance: south wall (z2), centered 5-wide × 4-tall open archway (no doors)
        var midX = (x1 + x2) / 2;
        // Clear a 5-wide × 4-tall air gap for a grand open archway
        await rcon.SendCommandAsync(
            $"fill {midX - 2} {y + 1} {z2} {midX + 2} {y + 4} {z2} minecraft:air", ct);
        // Stone brick threshold step outside the archway
        await rcon.SendCommandAsync(
            $"fill {midX - 2} {y} {z2 + 1} {midX + 2} {y} {z2 + 1} minecraft:stone_bricks", ct);
        // Decorative arch: stone brick stairs curving inward with chiseled keystone
        await rcon.SendCommandAsync(
            $"setblock {midX - 3} {y + 5} {z2} minecraft:stone_brick_stairs[facing=east,half=top]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX - 2} {y + 5} {z2} minecraft:stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX - 1} {y + 5} {z2} minecraft:stone_brick_stairs[facing=east,half=top]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 5} {z2} minecraft:chiseled_stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX + 1} {y + 5} {z2} minecraft:stone_brick_stairs[facing=west,half=top]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX + 2} {y + 5} {z2} minecraft:stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX + 3} {y + 5} {z2} minecraft:stone_brick_stairs[facing=west,half=top]", ct);
        // Lanterns flanking the archway
        await rcon.SendCommandAsync(
            $"setblock {midX - 3} {y + 1} {z2 + 1} minecraft:lantern[hanging=false]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX + 3} {y + 1} {z2 + 1} minecraft:lantern[hanging=false]", ct);

        logger.LogInformation("Tower exterior complete");
    }

    // =========================================================================
    // FLOOR PLATFORMS
    // =========================================================================

    private async Task BuildFloorPlatformsAsync(int x1, int y, int z1, int x2, int z2, CancellationToken ct)
    {
        // Floor 2 platform at y+7 (oak planks) — extends to interior edges (x1+1, x2-1, z1+1, z2-1)
        await rcon.SendCommandAsync(
            $"fill {x1 + 1} {y + 7} {z1 + 1} {x2 - 1} {y + 7} {z2 - 1} minecraft:oak_planks", ct);

        // Floor 3 platform at y+12 (oak planks)
        await rcon.SendCommandAsync(
            $"fill {x1 + 1} {y + 12} {z1 + 1} {x2 - 1} {y + 12} {z2 - 1} minecraft:oak_planks", ct);

        // Floor 4 platform at y+17 (deepslate tiles — prestige floor)
        await rcon.SendCommandAsync(
            $"fill {x1 + 1} {y + 17} {z1 + 1} {x2 - 1} {y + 17} {z2 - 1} minecraft:deepslate_tiles", ct);

        // Floor 5 / Roof platform at y+24 (oak planks — ceremonial landing)
        await rcon.SendCommandAsync(
            $"fill {x1 + 1} {y + 24} {z1 + 1} {x2 - 1} {y + 24} {z2 - 1} minecraft:oak_planks", ct);

        // Roof floor at y+31 (stone bricks — open battlements platform)
        // Already placed as parapet ring, just fill walkable area
        await rcon.SendCommandAsync(
            $"fill {x1 + 1} {y + 31} {z1 + 1} {x2 - 1} {y + 31} {z2 - 1} minecraft:stone_bricks", ct);

        logger.LogInformation("Tower floor platforms placed");
    }

    // =========================================================================
    // SPIRAL STAIRCASE — Counter-clockwise (left-handed)
    // South→East→North→West, individual setblock for facing direction
    // =========================================================================

    private async Task BuildSpiralStaircaseAsync(int x1, int y, int z1, int x2, int z2, CancellationToken ct)
    {
        // Clear stairwell holes in floor platforms FIRST so stairs can fill in solid parts
        await ClearStairwellHolesAsync(x1, y, z1, x2, z2, ct);

        // Flight 1: South wall, moving East (y+1 to y+7)
        // Stairs along z1+2 (inner south wall), x ascending eastward
        await BuildFlight1Async(x1, y, z1, ct);

        // Flight 2: East wall, moving North (y+8 to y+12)
        await BuildFlight2Async(x2, y, z1, ct);

        // Flight 3: North wall, moving West (y+13 to y+17)
        await BuildFlight3Async(x1, y, z2, ct);

        // Flight 4: West wall, moving South (y+18 to y+24)
        await BuildFlight4Async(x1, y, z1, ct);

        // Flight 5: Final climb — South wall again, to roof (y+25 to y+31)
        await BuildFlight5Async(x1, y, z1, ct);

        // Safety fences on inside edge of staircase flights
        await BuildStairFencesAsync(x1, y, z1, x2, z2, ct);

        // Lighting: lanterns along staircase
        await BuildStaircaseLightingAsync(x1, y, z1, x2, z2, ct);

        logger.LogInformation("Tower spiral staircase complete — 5 flights, continuous path");
    }

    /// <summary>
    /// Flight 1: Along SOUTH wall (z1+2/z1+3), ascending EAST.
    /// Steps from y+1 to y+7, x from x1+7 to x1+13 (centered on midX).
    /// 2 blocks wide for comfortable walking. facing=east.
    /// </summary>
    private async Task BuildFlight1Async(int x1, int y, int z1, CancellationToken ct)
    {
        // 2-wide staircase: row 1 at z1+2, row 2 at z1+3
        for (var i = 0; i < 7; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 7 + i} {y + 1 + i} {z1 + 2} minecraft:oak_stairs[facing=east]", ct);
            await rcon.SendCommandAsync(
                $"setblock {x1 + 7 + i} {y + 1 + i} {z1 + 3} minecraft:oak_stairs[facing=east]", ct);
        }
        // Landing platform at top (y+7, arrival to Floor 2)
        await rcon.SendCommandAsync(
            $"fill {x1 + 13} {y + 7} {z1 + 2} {x1 + 14} {y + 7} {z1 + 4} minecraft:oak_planks", ct);
    }

    /// <summary>
    /// Flight 2: Along EAST wall (x2-2/x2-3), ascending NORTH (increasing Z).
    /// Steps from y+8 to y+12, z from z1+8 to z1+12 (centered on midZ).
    /// 2 blocks wide. facing=south.
    /// </summary>
    private async Task BuildFlight2Async(int x2, int y, int z1, CancellationToken ct)
    {
        // 2-wide staircase: row 1 at x2-2, row 2 at x2-3
        for (var i = 0; i < 5; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x2 - 2} {y + 8 + i} {z1 + 8 + i} minecraft:oak_stairs[facing=south]", ct);
            await rcon.SendCommandAsync(
                $"setblock {x2 - 3} {y + 8 + i} {z1 + 8 + i} minecraft:oak_stairs[facing=south]", ct);
        }
        // Landing platform at top (y+12, arrival to Floor 3)
        await rcon.SendCommandAsync(
            $"fill {x2 - 4} {y + 12} {z1 + 12} {x2 - 2} {y + 12} {z1 + 13} minecraft:oak_planks", ct);
    }

    /// <summary>
    /// Flight 3: Along NORTH wall (z2-2/z2-3), ascending WEST (decreasing X).
    /// Steps from y+13 to y+17, x from x2-8 to x2-12 (centered on midX, walking west).
    /// 2 blocks wide. facing=west.
    /// </summary>
    private async Task BuildFlight3Async(int x1, int y, int z2, CancellationToken ct)
    {
        var x2 = TowerMaxX;
        // 2-wide staircase: row 1 at z2-2, row 2 at z2-3
        for (var i = 0; i < 5; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x2 - 8 - i} {y + 13 + i} {z2 - 2} minecraft:oak_stairs[facing=west]", ct);
            await rcon.SendCommandAsync(
                $"setblock {x2 - 8 - i} {y + 13 + i} {z2 - 3} minecraft:oak_stairs[facing=west]", ct);
        }
        // Landing platform at top (y+17, arrival to Floor 4)
        await rcon.SendCommandAsync(
            $"fill {x2 - 13} {y + 17} {z2 - 4} {x2 - 12} {y + 17} {z2 - 2} minecraft:deepslate_tiles", ct);
    }

    /// <summary>
    /// Flight 4: Along WEST wall (x1+2/x1+3), ascending SOUTH (decreasing Z).
    /// Steps from y+18 to y+24, z from z2-7 to z2-13 (centered on midZ, walking south/decreasing Z).
    /// 2 blocks wide. facing=north.
    /// </summary>
    private async Task BuildFlight4Async(int x1, int y, int z1, CancellationToken ct)
    {
        var z2 = TowerMaxZ;
        // 2-wide staircase: row 1 at x1+2, row 2 at x1+3
        for (var i = 0; i < 7; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 2} {y + 18 + i} {z2 - 7 - i} minecraft:oak_stairs[facing=north]", ct);
            await rcon.SendCommandAsync(
                $"setblock {x1 + 3} {y + 18 + i} {z2 - 7 - i} minecraft:oak_stairs[facing=north]", ct);
        }
        // Landing platform at top (y+24, arrival to Floor 5)
        await rcon.SendCommandAsync(
            $"fill {x1 + 2} {y + 24} {z2 - 14} {x1 + 4} {y + 24} {z2 - 13} minecraft:oak_planks", ct);
    }

    /// <summary>
    /// Flight 5: Along SOUTH wall again (z1+2/z1+3), ascending EAST to roof.
    /// Steps from y+25 to y+31, x from x1+7 to x1+13 (centered on midX).
    /// 2 blocks wide. facing=east.
    /// </summary>
    private async Task BuildFlight5Async(int x1, int y, int z1, CancellationToken ct)
    {
        // 2-wide staircase: row 1 at z1+2, row 2 at z1+3 (same as Flight 1)
        for (var i = 0; i < 7; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 7 + i} {y + 25 + i} {z1 + 2} minecraft:oak_stairs[facing=east]", ct);
            await rcon.SendCommandAsync(
                $"setblock {x1 + 7 + i} {y + 25 + i} {z1 + 3} minecraft:oak_stairs[facing=east]", ct);
        }
        // Landing platform at top (y+31, arrival to rooftop)
        await rcon.SendCommandAsync(
            $"fill {x1 + 13} {y + 31} {z1 + 2} {x1 + 14} {y + 31} {z1 + 4} minecraft:stone_bricks", ct);
    }

    /// <summary>
    /// Clear stairwell holes in floor platforms so stairs pass through.
    /// Wider holes (5×4 minimum) to accommodate 2-wide stairs and landing platforms.
    /// </summary>
    private async Task ClearStairwellHolesAsync(int x1, int y, int z1, int x2, int z2, CancellationToken ct)
    {
        // Hole in Floor 2 (y+7) for Flight 1 arrival / Flight 2 departure
        // Flight 1 arrives at x1+13, z1+2-3; Flight 2 departs from x2-2 to x2-3, z1+8
        // Clear a 5×5 opening around the corner
        await rcon.SendCommandAsync(
            $"fill {x1 + 12} {y + 7} {z1 + 2} {x1 + 16} {y + 7} {z1 + 6} minecraft:air", ct);

        // Hole in Floor 3 (y+12) for Flight 2 arrival / Flight 3 departure
        // Flight 2 arrives at x2-2 to x2-3, z1+12; Flight 3 departs from x2-8, z2-2 to z2-3
        await rcon.SendCommandAsync(
            $"fill {x2 - 6} {y + 12} {z1 + 11} {x2 - 2} {y + 12} {z1 + 15} minecraft:air", ct);

        // Hole in Floor 4 (y+17) for Flight 3 arrival / Flight 4 departure
        // Flight 3 arrives at x2-12, z2-2 to z2-3; Flight 4 departs from x1+2 to x1+3, z2-7
        await rcon.SendCommandAsync(
            $"fill {x2 - 14} {y + 17} {z2 - 6} {x2 - 10} {y + 17} {z2 - 2} minecraft:air", ct);

        // Hole in Floor 5 (y+24) for Flight 4 arrival / Flight 5 departure
        // Flight 4 arrives at x1+2 to x1+3, z2-13; Flight 5 departs from x1+7, z1+2 to z1+3
        await rcon.SendCommandAsync(
            $"fill {x1 + 2} {y + 24} {z2 - 15} {x1 + 6} {y + 24} {z2 - 11} minecraft:air", ct);

        // Hole in roof (y+31) for Flight 5 arrival
        // Flight 5 arrives at x1+13, z1+2 to z1+3
        await rcon.SendCommandAsync(
            $"fill {x1 + 12} {y + 31} {z1 + 1} {x1 + 16} {y + 31} {z1 + 5} minecraft:air", ct);
    }

    /// <summary>
    /// Safety fences on the inside edge of each staircase flight (prevent falling into central shaft).
    /// Fences run along the open side (toward tower center) of the 2-wide stairs.
    /// </summary>
    private async Task BuildStairFencesAsync(int x1, int y, int z1, int x2, int z2, CancellationToken ct)
    {
        // Flight 1 fence: south wall stairs (z1+2 and z1+3), fence on inside edge (z1+4)
        for (var i = 0; i < 7; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 7 + i} {y + 2 + i} {z1 + 4} minecraft:oak_fence", ct);
        }

        // Flight 2 fence: east wall stairs (x2-2 and x2-3), fence on inside edge (x2-4)
        for (var i = 0; i < 5; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x2 - 4} {y + 9 + i} {z1 + 8 + i} minecraft:oak_fence", ct);
        }

        // Flight 3 fence: north wall stairs (z2-2 and z2-3), fence on inside edge (z2-4)
        for (var i = 0; i < 5; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x2 - 8 - i} {y + 14 + i} {z2 - 4} minecraft:oak_fence", ct);
        }

        // Flight 4 fence: west wall stairs (x1+2 and x1+3), fence on inside edge (x1+4)
        for (var i = 0; i < 7; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 4} {y + 19 + i} {z2 - 7 - i} minecraft:oak_fence", ct);
        }

        // Flight 5 fence: south wall stairs again (z1+2 and z1+3), fence on inside edge (z1+4)
        for (var i = 0; i < 7; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 7 + i} {y + 26 + i} {z1 + 4} minecraft:oak_fence", ct);
        }
    }

    /// <summary>
    /// Lighting along staircase — wall torches every other step.
    /// </summary>
    private async Task BuildStaircaseLightingAsync(int x1, int y, int z1, int x2, int z2, CancellationToken ct)
    {
        // Flight 1: wall torches on south wall (behind stairs at z1+2)
        for (var i = 0; i < 7; i += 2)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 7 + i} {y + 3 + i} {z1 + 1} minecraft:wall_torch[facing=north]", ct);
        }

        // Flight 2: wall torches on east wall (behind stairs at x2-2)
        for (var i = 0; i < 5; i += 2)
        {
            await rcon.SendCommandAsync(
                $"setblock {x2 - 1} {y + 10 + i} {z1 + 8 + i} minecraft:wall_torch[facing=west]", ct);
        }

        // Flight 3: wall torches on north wall (behind stairs at z2-2)
        for (var i = 0; i < 5; i += 2)
        {
            await rcon.SendCommandAsync(
                $"setblock {x2 - 8 - i} {y + 15 + i} {z2 - 1} minecraft:wall_torch[facing=south]", ct);
        }

        // Flight 4: wall torches on west wall (behind stairs at x1+2)
        for (var i = 0; i < 7; i += 2)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 1} {y + 20 + i} {z2 - 7 - i} minecraft:wall_torch[facing=east]", ct);
        }

        // Flight 5: wall torches on south wall (behind stairs at z1+2)
        for (var i = 0; i < 7; i += 2)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 7 + i} {y + 27 + i} {z1 + 1} minecraft:wall_torch[facing=north]", ct);
        }
    }

    // =========================================================================
    // FLOOR 1: ENTRANCE HALL (y+1 to y+6)
    // =========================================================================

    private async Task BuildFloor1EntranceHallAsync(int x1, int y, int z1, int x2, int z2, CancellationToken ct)
    {
        var midX = (x1 + x2) / 2;
        var midZ = (z1 + z2) / 2;

        // Open archway and exterior lanterns are placed in BuildExteriorAsync

        // 4 lanterns around entry for ambiance
        await rcon.SendCommandAsync(
            $"setblock {x1 + 3} {y + 3} {z1 + 3} minecraft:lantern[hanging=false]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x2 - 3} {y + 3} {z1 + 3} minecraft:lantern[hanging=false]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x1 + 3} {y + 3} {z2 - 3} minecraft:lantern[hanging=false]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x2 - 3} {y + 3} {z2 - 3} minecraft:lantern[hanging=false]", ct);

        // Mossy stone details at base for aged look
        await rcon.SendCommandAsync(
            $"fill {x1 + 1} {y + 1} {z1 + 1} {x1 + 1} {y + 1} {z2 - 1} minecraft:mossy_stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x2 - 1} {y + 1} {z1 + 1} {x2 - 1} {y + 1} {z2 - 1} minecraft:mossy_stone_bricks", ct);

        logger.LogInformation("Floor 1 (Entrance Hall) furnished");
    }

    // =========================================================================
    // FLOOR 2: LIBRARY / CARTOGRAPHY ROOM (y+7 to y+11)
    // =========================================================================

    private async Task BuildFloor2LibraryAsync(int x1, int y, int z1, int x2, int z2, CancellationToken ct)
    {
        // Bookshelves lining walls (1 block inset from perimeter, full-height stacks)
        // South wall bookshelf row
        await rcon.SendCommandAsync(
            $"fill {x1 + 4} {y + 8} {z1 + 2} {x2 - 4} {y + 10} {z1 + 2} minecraft:bookshelf", ct);
        // North wall bookshelf row
        await rcon.SendCommandAsync(
            $"fill {x1 + 4} {y + 8} {z2 - 2} {x2 - 4} {y + 10} {z2 - 2} minecraft:bookshelf", ct);
        // West wall bookshelf column
        await rcon.SendCommandAsync(
            $"fill {x1 + 2} {y + 8} {z1 + 4} {x1 + 2} {y + 10} {z2 - 4} minecraft:bookshelf", ct);

        // Enchanting table in the center
        var midX = (x1 + x2) / 2;
        var midZ = (z1 + z2) / 2;
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 8} {midZ} minecraft:enchanting_table", ct);

        // Lecterns / writing desks (oak stairs as seats)
        await rcon.SendCommandAsync(
            $"setblock {midX - 2} {y + 8} {midZ + 2} minecraft:lectern[facing=north]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX + 2} {y + 8} {midZ + 2} minecraft:lectern[facing=north]", ct);
        // Oak stair seats facing lecterns
        await rcon.SendCommandAsync(
            $"setblock {midX - 2} {y + 8} {midZ + 3} minecraft:oak_stairs[facing=north]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX + 2} {y + 8} {midZ + 3} minecraft:oak_stairs[facing=north]", ct);

        // Hanging lanterns for reading light
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 11} {midZ - 3} minecraft:chain", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 10} {midZ - 3} minecraft:lantern[hanging=true]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 11} {midZ + 3} minecraft:chain", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 10} {midZ + 3} minecraft:lantern[hanging=true]", ct);

        logger.LogInformation("Floor 2 (Library) furnished");
    }

    // =========================================================================
    // FLOOR 3: ARMORY / BEACON CHAMBER (y+12 to y+16)
    // =========================================================================

    private async Task BuildFloor3ArmoryAsync(int x1, int y, int z1, int x2, int z2, CancellationToken ct)
    {
        var midX = (x1 + x2) / 2;
        var midZ = (z1 + z2) / 2;

        // Beacon monument in center: 3×3 iron block base at y+13, beacon at y+14
        await rcon.SendCommandAsync(
            $"fill {midX - 1} {y + 13} {midZ - 1} {midX + 1} {y + 13} {midZ + 1} minecraft:iron_block", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 14} {midZ} minecraft:beacon", ct);
        // Clear above beacon for beam
        await rcon.SendCommandAsync(
            $"fill {midX} {y + 15} {midZ} {midX} {y + 16} {midZ} minecraft:air", ct);

        // Armor stands at corners
        await rcon.SendCommandAsync(
            $"summon minecraft:armor_stand {x1 + 4} {y + 13} {z1 + 4}", ct);
        await rcon.SendCommandAsync(
            $"summon minecraft:armor_stand {x2 - 4} {y + 13} {z1 + 4}", ct);
        await rcon.SendCommandAsync(
            $"summon minecraft:armor_stand {x1 + 4} {y + 13} {z2 - 4}", ct);
        await rcon.SendCommandAsync(
            $"summon minecraft:armor_stand {x2 - 4} {y + 13} {z2 - 4}", ct);

        // Double chests for supplies (east wall)
        await rcon.SendCommandAsync(
            $"setblock {x2 - 2} {y + 13} {midZ - 1} minecraft:chest[facing=west,type=left]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x2 - 2} {y + 13} {midZ} minecraft:chest[facing=west,type=right]", ct);

        // Stained glass accents on walls
        await rcon.SendCommandAsync(
            $"fill {x1 + 5} {y + 14} {z1} {x1 + 6} {y + 15} {z1} minecraft:light_blue_stained_glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x2 - 6} {y + 14} {z1} {x2 - 5} {y + 15} {z1} minecraft:purple_stained_glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x1 + 5} {y + 14} {z2} {x1 + 6} {y + 15} {z2} minecraft:light_blue_stained_glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x2 - 6} {y + 14} {z2} {x2 - 5} {y + 15} {z2} minecraft:purple_stained_glass_pane", ct);

        // Banners on walls
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 15} {z1 + 1} minecraft:purple_wall_banner[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 15} {z2 - 1} minecraft:purple_wall_banner[facing=north]", ct);

        // Floor lanterns
        await rcon.SendCommandAsync(
            $"setblock {x1 + 3} {y + 13} {z1 + 3} minecraft:lantern[hanging=false]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x2 - 3} {y + 13} {z2 - 3} minecraft:lantern[hanging=false]", ct);

        logger.LogInformation("Floor 3 (Armory/Beacon) furnished");
    }

    // =========================================================================
    // FLOOR 4: OBSERVATION GALLERY (y+17 to y+23)
    // =========================================================================

    private async Task BuildFloor4ObservatoryAsync(int x1, int y, int z1, int x2, int z2, CancellationToken ct)
    {
        var midX = (x1 + x2) / 2;
        var midZ = (z1 + z2) / 2;

        // Observation benches (stairs facing outward toward windows)
        // South-facing benches
        await rcon.SendCommandAsync(
            $"fill {x1 + 4} {y + 18} {z1 + 3} {x1 + 6} {y + 18} {z1 + 3} minecraft:oak_stairs[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"fill {x2 - 6} {y + 18} {z1 + 3} {x2 - 4} {y + 18} {z1 + 3} minecraft:oak_stairs[facing=south]", ct);
        // North-facing benches
        await rcon.SendCommandAsync(
            $"fill {x1 + 4} {y + 18} {z2 - 3} {x1 + 6} {y + 18} {z2 - 3} minecraft:oak_stairs[facing=north]", ct);
        await rcon.SendCommandAsync(
            $"fill {x2 - 6} {y + 18} {z2 - 3} {x2 - 4} {y + 18} {z2 - 3} minecraft:oak_stairs[facing=north]", ct);
        // West-facing benches
        await rcon.SendCommandAsync(
            $"fill {x1 + 3} {y + 18} {z1 + 4} {x1 + 3} {y + 18} {z1 + 6} minecraft:oak_stairs[facing=west]", ct);
        // East-facing benches
        await rcon.SendCommandAsync(
            $"fill {x2 - 3} {y + 18} {z1 + 4} {x2 - 3} {y + 18} {z1 + 6} minecraft:oak_stairs[facing=east]", ct);

        // Telescope lectern facing south (toward town)
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 18} {z1 + 4} minecraft:lectern[facing=south]", ct);

        // Wool/banner decorations with town colors
        await rcon.SendCommandAsync(
            $"setblock {midX - 3} {y + 20} {z1 + 1} minecraft:blue_wall_banner[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX + 3} {y + 20} {z1 + 1} minecraft:blue_wall_banner[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX - 3} {y + 20} {z2 - 1} minecraft:blue_wall_banner[facing=north]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX + 3} {y + 20} {z2 - 1} minecraft:blue_wall_banner[facing=north]", ct);

        // Hanging lanterns for ambiance
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 22} {midZ} minecraft:chain", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 21} {midZ} minecraft:lantern[hanging=true]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x1 + 4} {y + 22} {midZ} minecraft:chain", ct);
        await rcon.SendCommandAsync(
            $"setblock {x1 + 4} {y + 21} {midZ} minecraft:lantern[hanging=true]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x2 - 4} {y + 22} {midZ} minecraft:chain", ct);
        await rcon.SendCommandAsync(
            $"setblock {x2 - 4} {y + 21} {midZ} minecraft:lantern[hanging=true]", ct);

        logger.LogInformation("Floor 4 (Observation Gallery) furnished");
    }

    // =========================================================================
    // FLOOR 5: ROOFTOP / CROWNING CHAMBER (y+24 to y+32)
    // =========================================================================

    private async Task BuildFloor5RooftopAsync(int x1, int y, int z1, int x2, int z2, CancellationToken ct)
    {
        var midX = (x1 + x2) / 2;
        var midZ = (z1 + z2) / 2;

        // Compass markers at cardinal directions (wool blocks on roof floor y+31)
        // North = red
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 32} {z2 - 1} minecraft:red_wool", ct);
        // South = blue
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 32} {z1 + 1} minecraft:blue_wool", ct);
        // East = green
        await rcon.SendCommandAsync(
            $"setblock {x2 - 1} {y + 32} {midZ} minecraft:green_wool", ct);
        // West = yellow
        await rcon.SendCommandAsync(
            $"setblock {x1 + 1} {y + 32} {midZ} minecraft:yellow_wool", ct);

        // Central flagpole: oak fence post + banner
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 32} {midZ} minecraft:oak_fence", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 33} {midZ} minecraft:oak_fence", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 34} {midZ} minecraft:white_banner[rotation=0]", ct);

        // Signal beacon at pinnacle: sea lantern atop flagpole
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 35} {midZ} minecraft:sea_lantern", ct);

        // Corner pinnacle lanterns (atop corner buttresses)
        await rcon.SendCommandAsync(
            $"setblock {x1 + 1} {y + 35} {z1 + 1} minecraft:lantern[hanging=false]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x2 - 1} {y + 35} {z1 + 1} minecraft:lantern[hanging=false]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x1 + 1} {y + 35} {z2 - 1} minecraft:lantern[hanging=false]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x2 - 1} {y + 35} {z2 - 1} minecraft:lantern[hanging=false]", ct);

        // Decorative banners on inner parapet walls
        await rcon.SendCommandAsync(
            $"setblock {midX - 4} {y + 30} {z1 + 1} minecraft:light_blue_wall_banner[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX + 4} {y + 30} {z1 + 1} minecraft:light_blue_wall_banner[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX - 4} {y + 30} {z2 - 1} minecraft:light_blue_wall_banner[facing=north]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX + 4} {y + 30} {z2 - 1} minecraft:light_blue_wall_banner[facing=north]", ct);

        // Glowstone corner accents (ambient light on roof)
        await rcon.SendCommandAsync(
            $"setblock {x1 + 3} {y + 25} {z1 + 3} minecraft:glowstone", ct);
        await rcon.SendCommandAsync(
            $"setblock {x2 - 3} {y + 25} {z1 + 3} minecraft:glowstone", ct);
        await rcon.SendCommandAsync(
            $"setblock {x1 + 3} {y + 25} {z2 - 3} minecraft:glowstone", ct);
        await rcon.SendCommandAsync(
            $"setblock {x2 - 3} {y + 25} {z2 - 3} minecraft:glowstone", ct);

        logger.LogInformation("Floor 5 (Rooftop/Crowning Chamber) furnished");
    }

    // =========================================================================
    // COBBLESTONE WALKWAY: TOWER → VILLAGE GATE
    // =========================================================================

    /// <summary>
    /// Builds a 5-wide cobblestone walkway from the tower entrance south to the
    /// village fence gate, with stone brick wall borders and lanterns.
    /// </summary>
    private async Task BuildWalkwayAsync(int y, CancellationToken ct)
    {
        var gateWidth = VillageLayout.GateWidth;
        var walkX1 = _villageCenterX - gateWidth / 2;
        var walkX2 = walkX1 + gateWidth - 1;

        // Walkway runs from just outside the tower entrance (TowerMaxZ + 1)
        // through the fence gate and one block inside town (fenceMinZ + 1)
        var walkZStart = TowerMaxZ + 1;
        var walkZEnd = _fenceMinZ + 1;

        if (walkZEnd <= walkZStart) return; // No gap to fill

        // Cobblestone path surface at ground level
        await rcon.SendCommandAsync(
            $"fill {walkX1} {y} {walkZStart} {walkX2} {y} {walkZEnd} minecraft:cobblestone", ct);

        // Stone brick wall borders on both sides
        await rcon.SendCommandAsync(
            $"fill {walkX1 - 1} {y + 1} {walkZStart} {walkX1 - 1} {y + 1} {walkZEnd} minecraft:stone_brick_wall", ct);
        await rcon.SendCommandAsync(
            $"fill {walkX2 + 1} {y + 1} {walkZStart} {walkX2 + 1} {y + 1} {walkZEnd} minecraft:stone_brick_wall", ct);

        // Lanterns every 4 blocks along the edges for visibility
        for (var z = walkZStart; z <= walkZEnd; z += 4)
        {
            await rcon.SendCommandAsync(
                $"setblock {walkX1 - 1} {y + 2} {z} minecraft:lantern[hanging=false]", ct);
            await rcon.SendCommandAsync(
                $"setblock {walkX2 + 1} {y + 2} {z} minecraft:lantern[hanging=false]", ct);
        }

        logger.LogInformation("Walkway built from tower (Z={TowerZ}) to gate (Z={GateZ})",
            walkZStart, walkZEnd);
    }
}
