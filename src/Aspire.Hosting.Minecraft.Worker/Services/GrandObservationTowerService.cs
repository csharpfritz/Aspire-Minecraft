using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Builds a standalone 21×21, 32-block-tall Grand Observation Tower at the south entrance
/// of the Aspire village. Five themed floors connected by a continuous counter-clockwise
/// spiral staircase (oak stairs). Players walk from ground to roof without jumping.
///
/// Placement: x=20–40, z=-11 to 10, 32 blocks above SurfaceY.
/// Built once at startup, independent of resource structures.
/// </summary>
internal sealed class GrandObservationTowerService(
    RconService rcon,
    BuildingProtectionService protection,
    ILogger<GrandObservationTowerService> logger)
{
    private bool _built;

    // Tower absolute coordinates (independent of village BaseX/BaseZ).
    // Placed entirely south of the fence line (z=-10) with a 5-block gap.
    private const int TowerOriginX = 20;
    private const int TowerOriginZ = -36;
    private const int TowerSize = 21;       // 21×21 footprint
    private const int TowerHeight = 32;     // y+1 through y+32

    private static int TowerMaxX => TowerOriginX + TowerSize - 1;  // 40
    private static int TowerMaxZ => TowerOriginZ + TowerSize - 1;  // -16

    /// <summary>
    /// Forceloads the tower chunk area. Must be called before <see cref="BuildTowerAsync"/>.
    /// </summary>
    public async Task ForceloadAsync(CancellationToken ct)
    {
        await rcon.SendCommandAsync(
            $"forceload add {TowerOriginX - 2} {TowerOriginZ - 2} {TowerMaxX + 2} {TowerMaxZ + 2}", ct);
        logger.LogInformation("Forceloaded tower area: ({X1},{Z1}) to ({X2},{Z2})",
            TowerOriginX - 2, TowerOriginZ - 2, TowerMaxX + 2, TowerMaxZ + 2);
    }

    /// <summary>
    /// Registers the tower's 3D bounding box with the building protection service
    /// so canals, rails, and other subsystems avoid the tower volume.
    /// </summary>
    public void RegisterProtection()
    {
        var y = VillageLayout.SurfaceY;
        protection.Register(
            TowerOriginX - 1, y, TowerOriginZ - 2,
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

        logger.LogInformation("Building Grand Observation Tower at ({X},{Z})...", TowerOriginX, TowerOriginZ);
        using var burst = rcon.EnterBurstMode(40);

        var y = VillageLayout.SurfaceY;
        var x1 = TowerOriginX;
        var z1 = TowerOriginZ;
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

        _built = true;
        logger.LogInformation("Grand Observation Tower complete — 5 floors, spiral staircase, full decorations");
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

        // Entrance: south wall (z1), centered 3-wide × 4-tall opening
        var midX = (x1 + x2) / 2; // 30
        await rcon.SendCommandAsync(
            $"fill {midX - 1} {y + 1} {z1} {midX + 1} {y + 4} {z1} minecraft:air", ct);
        // Decorative arch above entrance
        await rcon.SendCommandAsync(
            $"setblock {midX - 2} {y + 5} {z1} minecraft:stone_brick_stairs[facing=east,half=top]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX + 2} {y + 5} {z1} minecraft:stone_brick_stairs[facing=west,half=top]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 5} {z1} minecraft:chiseled_stone_bricks", ct);

        logger.LogInformation("Tower exterior complete");
    }

    // =========================================================================
    // FLOOR PLATFORMS
    // =========================================================================

    private async Task BuildFloorPlatformsAsync(int x1, int y, int z1, int x2, int z2, CancellationToken ct)
    {
        // Floor 2 platform at y+7 (oak planks)
        await rcon.SendCommandAsync(
            $"fill {x1 + 2} {y + 7} {z1 + 2} {x2 - 2} {y + 7} {z2 - 2} minecraft:oak_planks", ct);

        // Floor 3 platform at y+12 (oak planks)
        await rcon.SendCommandAsync(
            $"fill {x1 + 2} {y + 12} {z1 + 2} {x2 - 2} {y + 12} {z2 - 2} minecraft:oak_planks", ct);

        // Floor 4 platform at y+17 (deepslate tiles — prestige floor)
        await rcon.SendCommandAsync(
            $"fill {x1 + 2} {y + 17} {z1 + 2} {x2 - 2} {y + 17} {z2 - 2} minecraft:deepslate_tiles", ct);

        // Floor 5 / Roof platform at y+24 (oak planks — ceremonial landing)
        await rcon.SendCommandAsync(
            $"fill {x1 + 2} {y + 24} {z1 + 2} {x2 - 2} {y + 24} {z2 - 2} minecraft:oak_planks", ct);

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

        // Clear stairwell holes in floor platforms so stairs connect
        await ClearStairwellHolesAsync(x1, y, z1, x2, z2, ct);

        // Safety fences on inside edge of staircase flights
        await BuildStairFencesAsync(x1, y, z1, x2, z2, ct);

        // Lighting: lanterns along staircase
        await BuildStaircaseLightingAsync(x1, y, z1, x2, z2, ct);

        logger.LogInformation("Tower spiral staircase complete — 5 flights, continuous path");
    }

    /// <summary>
    /// Flight 1: Along SOUTH wall (z1+2), ascending EAST.
    /// Steps from y+1 to y+7, x from x1+3 to x1+9.
    /// facing=east means the stairs ascend when walking east.
    /// </summary>
    private async Task BuildFlight1Async(int x1, int y, int z1, CancellationToken ct)
    {
        var z = z1 + 2;
        for (var i = 0; i < 7; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 3 + i} {y + 1 + i} {z} minecraft:oak_stairs[facing=east]", ct);
        }
    }

    /// <summary>
    /// Flight 2: Along EAST wall (x2-2), ascending NORTH (increasing Z).
    /// Steps from y+8 to y+12, z from z1+3 to z1+7.
    /// facing=south means the stairs ascend when walking south (increasing Z).
    /// </summary>
    private async Task BuildFlight2Async(int x2, int y, int z1, CancellationToken ct)
    {
        var x = x2 - 2;
        for (var i = 0; i < 5; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x} {y + 8 + i} {z1 + 3 + i} minecraft:oak_stairs[facing=south]", ct);
        }
    }

    /// <summary>
    /// Flight 3: Along NORTH wall (z2-2), ascending WEST (decreasing X).
    /// Steps from y+13 to y+17, x from x2-3 to x2-7 (moving west).
    /// facing=west means the stairs ascend when walking west.
    /// </summary>
    private async Task BuildFlight3Async(int x1, int y, int z2, CancellationToken ct)
    {
        var z = z2 - 2;
        var x2 = TowerMaxX;
        for (var i = 0; i < 5; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x2 - 3 - i} {y + 13 + i} {z} minecraft:oak_stairs[facing=west]", ct);
        }
    }

    /// <summary>
    /// Flight 4: Along WEST wall (x1+2), ascending SOUTH (decreasing Z).
    /// Steps from y+18 to y+24, z from z2-3 to z2-9 (moving south / decreasing Z).
    /// facing=north means the stairs ascend when walking north (decreasing Z direction).
    /// </summary>
    private async Task BuildFlight4Async(int x1, int y, int z1, CancellationToken ct)
    {
        var x = x1 + 2;
        var z2 = TowerMaxZ;
        for (var i = 0; i < 7; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x} {y + 18 + i} {z2 - 3 - i} minecraft:oak_stairs[facing=north]", ct);
        }
    }

    /// <summary>
    /// Flight 5: Along SOUTH wall again (z1+2), ascending EAST to roof.
    /// Steps from y+25 to y+31, x from x1+3 to x1+9.
    /// </summary>
    private async Task BuildFlight5Async(int x1, int y, int z1, CancellationToken ct)
    {
        var z = z1 + 2;
        for (var i = 0; i < 7; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 3 + i} {y + 25 + i} {z} minecraft:oak_stairs[facing=east]", ct);
        }
    }

    /// <summary>
    /// Clear stairwell holes in floor platforms so stairs pass through.
    /// </summary>
    private async Task ClearStairwellHolesAsync(int x1, int y, int z1, int x2, int z2, CancellationToken ct)
    {
        // Hole in Floor 2 (y+7) for Flight 1 arrival / Flight 2 departure (east side)
        await rcon.SendCommandAsync(
            $"fill {x2 - 4} {y + 7} {z1 + 2} {x2 - 2} {y + 7} {z1 + 5} minecraft:air", ct);

        // Hole in Floor 3 (y+12) for Flight 2 arrival / Flight 3 departure (north side)
        await rcon.SendCommandAsync(
            $"fill {x2 - 5} {y + 12} {z2 - 4} {x2 - 2} {y + 12} {z2 - 2} minecraft:air", ct);

        // Hole in Floor 4 (y+17) for Flight 3 arrival / Flight 4 departure (west side)
        await rcon.SendCommandAsync(
            $"fill {x1 + 2} {y + 17} {z2 - 5} {x1 + 4} {y + 17} {z2 - 2} minecraft:air", ct);

        // Hole in Floor 5 (y+24) for Flight 4 arrival / Flight 5 departure (south side)
        await rcon.SendCommandAsync(
            $"fill {x1 + 2} {y + 24} {z1 + 2} {x1 + 5} {y + 24} {z1 + 5} minecraft:air", ct);

        // Hole in roof (y+31) for Flight 5 arrival
        await rcon.SendCommandAsync(
            $"fill {x1 + 8} {y + 31} {z1 + 1} {x1 + 10} {y + 31} {z1 + 3} minecraft:air", ct);
    }

    /// <summary>
    /// Safety fences on the inside edge of each staircase flight (prevent falling into central shaft).
    /// </summary>
    private async Task BuildStairFencesAsync(int x1, int y, int z1, int x2, int z2, CancellationToken ct)
    {
        // Flight 1 fence: south wall stairs, fence on north side (z1+3)
        for (var i = 0; i < 7; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 3 + i} {y + 2 + i} {z1 + 3} minecraft:oak_fence", ct);
        }

        // Flight 2 fence: east wall stairs, fence on west side (x2-3)
        for (var i = 0; i < 5; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x2 - 3} {y + 9 + i} {z1 + 3 + i} minecraft:oak_fence", ct);
        }

        // Flight 3 fence: north wall stairs, fence on south side (z2-3)
        for (var i = 0; i < 5; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x2 - 3 - i} {y + 14 + i} {z2 - 3} minecraft:oak_fence", ct);
        }

        // Flight 4 fence: west wall stairs, fence on east side (x1+3)
        for (var i = 0; i < 7; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 3} {y + 19 + i} {z2 - 3 - i} minecraft:oak_fence", ct);
        }

        // Flight 5 fence: south wall stairs again, fence on north side (z1+3)
        for (var i = 0; i < 7; i++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 3 + i} {y + 26 + i} {z1 + 3} minecraft:oak_fence", ct);
        }
    }

    /// <summary>
    /// Lighting along staircase — lanterns every other step.
    /// </summary>
    private async Task BuildStaircaseLightingAsync(int x1, int y, int z1, int x2, int z2, CancellationToken ct)
    {
        // Lanterns on wall side of each flight (every 2 steps)
        // Flight 1: wall torches on south wall
        for (var i = 0; i < 7; i += 2)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 3 + i} {y + 3 + i} {z1 + 1} minecraft:wall_torch[facing=north]", ct);
        }

        // Flight 2: wall torches on east wall
        for (var i = 0; i < 5; i += 2)
        {
            await rcon.SendCommandAsync(
                $"setblock {x2 - 1} {y + 10 + i} {z1 + 3 + i} minecraft:wall_torch[facing=west]", ct);
        }

        // Flight 3: wall torches on north wall
        for (var i = 0; i < 5; i += 2)
        {
            await rcon.SendCommandAsync(
                $"setblock {x2 - 3 - i} {y + 15 + i} {z2 - 1} minecraft:wall_torch[facing=south]", ct);
        }

        // Flight 4: wall torches on west wall
        for (var i = 0; i < 7; i += 2)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 1} {y + 20 + i} {z2 - 3 - i} minecraft:wall_torch[facing=east]", ct);
        }

        // Flight 5: wall torches on south wall
        for (var i = 0; i < 7; i += 2)
        {
            await rcon.SendCommandAsync(
                $"setblock {x1 + 3 + i} {y + 27 + i} {z1 + 1} minecraft:wall_torch[facing=north]", ct);
        }
    }

    // =========================================================================
    // FLOOR 1: ENTRANCE HALL (y+1 to y+6)
    // =========================================================================

    private async Task BuildFloor1EntranceHallAsync(int x1, int y, int z1, int x2, int z2, CancellationToken ct)
    {
        var midX = (x1 + x2) / 2;
        var midZ = (z1 + z2) / 2;

        // Oak doors at entrance (south wall, y+1 and y+2)
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 1} {z1} minecraft:oak_door[facing=south,half=lower,hinge=left]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 2} {z1} minecraft:oak_door[facing=south,half=upper,hinge=left]", ct);

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

        // Welcome sign above entrance (inside)
        await rcon.SendCommandAsync(
            $"setblock {midX} {y + 4} {z1 + 1} minecraft:oak_wall_sign[facing=south]", ct);
        var signCmd = "data merge block " + $"{midX} {y + 4} {z1 + 1}" +
            " {front_text:{messages:[\"\"," +
            "'{\"text\":\"Grand Observation\",\"color\":\"gold\"}'," +
            "'{\"text\":\"Tower\",\"color\":\"gold\"}'," +
            "\"\"]}}";
        await rcon.SendCommandAsync(signCmd, ct);

        // Exterior lanterns flanking entrance
        await rcon.SendCommandAsync(
            $"setblock {midX - 2} {y + 3} {z1} minecraft:lantern[hanging=false]", ct);
        await rcon.SendCommandAsync(
            $"setblock {midX + 2} {y + 3} {z1} minecraft:lantern[hanging=false]", ct);

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
}
