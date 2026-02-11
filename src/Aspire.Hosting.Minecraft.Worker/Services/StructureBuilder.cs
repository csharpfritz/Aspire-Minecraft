using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Builds themed village structures in the Minecraft world representing each Aspire resource.
/// Structure type is selected by resource type: Project=Watchtower, Container=Warehouse,
/// Executable=Workshop, Unknown=Cottage. Laid out in a 2×N grid with cobblestone paths.
/// </summary>
internal sealed class StructureBuilder(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<StructureBuilder> logger)
{
    private bool _pathsBuilt;
    private bool _fenceBuilt;
    private readonly HashSet<string> _builtStructures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Builds or updates structures for all monitored resources.
    /// </summary>
    public async Task UpdateStructuresAsync(CancellationToken ct = default)
    {
        using var activity = MinecraftMetrics.ActivitySource.StartActivity("minecraft.world.update_structures");

        try
        {
            if (!_fenceBuilt && monitor.TotalCount > 0)
            {
                await BuildFencePerimeterAsync(monitor.TotalCount, ct);
                _fenceBuilt = true;
            }

            if (!_pathsBuilt && monitor.TotalCount > 0)
            {
                await BuildPathsAsync(monitor.TotalCount, ct);
                _pathsBuilt = true;
            }

            var index = 0;
            foreach (var (name, info) in monitor.Resources)
            {
                if (!_builtStructures.Contains(name))
                {
                    await BuildResourceStructureAsync(info, index, ct);
                    _builtStructures.Add(name);
                }
                else
                {
                    // Only update health indicator, not rebuild entire structure
                    try
                    {
                        var (x, y, z) = VillageLayout.GetStructureOrigin(index);
                        var structureType = GetStructureType(info.Type);
                        await PlaceHealthIndicatorAsync(x, y, z, structureType, info.Status, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to update health indicator for {ResourceName}", name);
                    }
                }
                index++;
            }

            logger.LogInformation("Village structures updated for {Count} resources", monitor.TotalCount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update structures");
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
        }
    }

    /// <summary>
    /// Selects the structure type based on Aspire resource type.
    /// </summary>
    internal static string GetStructureType(string resourceType)
    {
        return resourceType.ToLowerInvariant() switch
        {
            "project" => "Watchtower",
            "container" => "Warehouse",
            "executable" => "Workshop",
            _ => "Cottage"
        };
    }

    private async Task BuildFencePerimeterAsync(int resourceCount, CancellationToken ct)
    {
        try
        {
            var (fMinX, fMinZ, fMaxX, fMaxZ) = VillageLayout.GetFencePerimeter(resourceCount);
            var fenceY = VillageLayout.BaseY;

            // South side (low Z) — two segments with a gate gap in the center
            var gateX = VillageLayout.BaseX + VillageLayout.StructureSize; // center of the boulevard
            await rcon.SendCommandAsync(
                $"fill {fMinX} {fenceY} {fMinZ} {gateX - 1} {fenceY} {fMinZ} minecraft:oak_fence", ct);
            await rcon.SendCommandAsync(
                $"fill {gateX + 2} {fenceY} {fMinZ} {fMaxX} {fenceY} {fMinZ} minecraft:oak_fence", ct);
            // Gate (3-wide to match the boulevard width)
            await rcon.SendCommandAsync(
                $"fill {gateX} {fenceY} {fMinZ} {gateX + 2} {fenceY} {fMinZ} minecraft:oak_fence_gate[facing=south]", ct);

            // North side (high Z)
            await rcon.SendCommandAsync(
                $"fill {fMinX} {fenceY} {fMaxZ} {fMaxX} {fenceY} {fMaxZ} minecraft:oak_fence", ct);

            // West side (low X) — skip south and north corners (already placed)
            await rcon.SendCommandAsync(
                $"fill {fMinX} {fenceY} {fMinZ + 1} {fMinX} {fenceY} {fMaxZ - 1} minecraft:oak_fence", ct);

            // East side (high X) — skip south and north corners
            await rcon.SendCommandAsync(
                $"fill {fMaxX} {fenceY} {fMinZ + 1} {fMaxX} {fenceY} {fMaxZ - 1} minecraft:oak_fence", ct);
            
            logger.LogInformation("Fence perimeter built successfully");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to build fence perimeter - continuing with remaining structures");
        }
    }

    private async Task BuildPathsAsync(int resourceCount, CancellationToken ct)
    {
        try
        {
            var rows = (resourceCount + VillageLayout.Columns - 1) / VillageLayout.Columns;
            var (fMinX, fMinZ, fMaxX, fMaxZ) = VillageLayout.GetFencePerimeter(resourceCount);

            // Comprehensive cobblestone coverage: entire village area inside fence, flush with ground
            // Step 1: Clear all grass blocks at BaseY (surface level)
            await rcon.SendCommandAsync(
                $"fill {fMinX + 1} {VillageLayout.BaseY} {fMinZ + 1} {fMaxX - 1} {VillageLayout.BaseY} {fMaxZ - 1} minecraft:air replace grass_block", ct);

            // Step 2: Place cobblestone at BaseY-1 (one level down, flush with remaining grass)
            await rcon.SendCommandAsync(
                $"fill {fMinX + 1} {VillageLayout.BaseY - 1} {fMinZ + 1} {fMaxX - 1} {VillageLayout.BaseY - 1} {fMaxZ - 1} minecraft:cobblestone", ct);

            logger.LogInformation("Comprehensive village paths built covering fence interior");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to build paths - continuing with remaining structures");
        }
    }

    private async Task BuildResourceStructureAsync(ResourceInfo info, int index, CancellationToken ct)
    {
        try
        {
            var (x, y, z) = VillageLayout.GetStructureOrigin(index);

            var structureType = GetStructureType(info.Type);

            switch (structureType)
            {
                case "Watchtower":
                    await BuildWatchtowerAsync(x, y, z, ct);
                    break;
                case "Warehouse":
                    await BuildWarehouseAsync(x, y, z, ct);
                    break;
                case "Workshop":
                    await BuildWorkshopAsync(x, y, z, ct);
                    break;
                default:
                    await BuildCottageAsync(x, y, z, ct);
                    break;
            }

            // Health indicator: redstone lamp in wall, powered = healthy
            await PlaceHealthIndicatorAsync(x, y, z, structureType, info.Status, ct);

            // Sign with resource name at the entrance
            await PlaceSignAsync(x, y, z, info, ct);

            logger.LogInformation("Village structure built: {ResourceName} ({StructureType}) at ({X},{Y},{Z})",
                info.Name, structureType, x, y, z);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to build structure for {ResourceName} - continuing with next resource", info.Name);
        }
    }

    /// <summary>
    /// Watchtower — tall, narrow tower with flag. Project (.NET app) resources.
    /// Stone brick walls, blue wool/banner accents, ~7×7 footprint, 10 blocks tall.
    /// </summary>
    private async Task BuildWatchtowerAsync(int x, int y, int z, CancellationToken ct)
    {
        // Foundation: 7×7 stone brick floor
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z} {x + 6} {y} {z + 6} minecraft:stone_bricks", ct);

        // Walls: 5×5 hollow stone brick tower (inside the 7×7), 8 blocks tall
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 1} {z + 1} {x + 5} {y + 8} {z + 5} minecraft:stone_bricks hollow", ct);

        // Corner pillars: extend to full height with stone brick
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 1} {z + 1} {x + 1} {y + 9} {z + 1} minecraft:stone_brick_stairs[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 5} {y + 1} {z + 1} {x + 5} {y + 9} {z + 1} minecraft:stone_brick_stairs[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 1} {z + 5} {x + 1} {y + 9} {z + 5} minecraft:stone_brick_stairs[facing=north]", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 5} {y + 1} {z + 5} {x + 5} {y + 9} {z + 5} minecraft:stone_brick_stairs[facing=north]", ct);

        // Windows (one on each side, at y+4)
        await rcon.SendCommandAsync(
            $"setblock {x + 3} {y + 4} {z + 1} minecraft:glass_pane", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 3} {y + 4} {z + 5} minecraft:glass_pane", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 4} {z + 3} minecraft:glass_pane", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 5} {y + 4} {z + 3} minecraft:glass_pane", ct);

        // Blue wool trim at top
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 8} {z + 1} {x + 5} {y + 8} {z + 5} minecraft:blue_wool", ct);

        // Roof cap (3×3 stone brick slab)
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y + 9} {z + 2} {x + 4} {y + 9} {z + 4} minecraft:stone_brick_slab", ct);

        // Flag pole + banner on top
        await rcon.SendCommandAsync(
            $"fill {x + 3} {y + 9} {z + 3} {x + 3} {y + 10} {z + 3} minecraft:oak_fence", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 3} {y + 10} {z + 2} minecraft:blue_banner[rotation=0]", ct);

        // Door opening (front face, Z-min side) - 2 blocks wide, 3 tall
        // Watchtower has 5x5 hollow starting at x+1,z+1, so front wall is at z+1
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y + 1} {z + 1} {x + 4} {y + 3} {z + 1} minecraft:air", ct);
    }

    /// <summary>
    /// Warehouse — wide, flat, cargo bay feel. Container (Docker) resources.
    /// Iron block frame, purple stained glass windows, ~7×7 footprint, 5 blocks tall.
    /// </summary>
    private async Task BuildWarehouseAsync(int x, int y, int z, CancellationToken ct)
    {
        // Foundation: 7×7 iron block floor
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z} {x + 6} {y} {z + 6} minecraft:iron_block", ct);

        // Walls: hollow box, 4 blocks tall
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z} {x + 6} {y + 4} {z + 6} minecraft:iron_block hollow", ct);

        // Flat roof
        await rcon.SendCommandAsync(
            $"fill {x} {y + 5} {z} {x + 6} {y + 5} {z + 6} minecraft:iron_block", ct);

        // Wide cargo door (front face, 3 wide × 3 tall) - ensure on actual wall face
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y + 1} {z} {x + 4} {y + 3} {z} minecraft:air", ct);

        // Purple stained glass windows on sides
        await rcon.SendCommandAsync(
            $"fill {x} {y + 3} {z + 2} {x} {y + 3} {z + 4} minecraft:purple_stained_glass", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 6} {y + 3} {z + 2} {x + 6} {y + 3} {z + 4} minecraft:purple_stained_glass", ct);

        // Purple stained glass on back wall
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y + 3} {z + 6} {x + 4} {y + 3} {z + 6} minecraft:purple_stained_glass", ct);

        // Interior: barrel storage
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 1} {z + 5} minecraft:barrel[facing=up]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 5} {y + 1} {z + 5} minecraft:barrel[facing=up]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 1} {z + 3} minecraft:barrel[facing=up]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 5} {y + 1} {z + 3} minecraft:barrel[facing=up]", ct);
    }

    /// <summary>
    /// Workshop — small building with chimney and workbench vibe. Executable resources.
    /// Oak planks walls, cyan stained glass accents, ~7×7 footprint, 6 blocks tall.
    /// </summary>
    private async Task BuildWorkshopAsync(int x, int y, int z, CancellationToken ct)
    {
        // Foundation: 7×7 oak planks floor
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z} {x + 6} {y} {z + 6} minecraft:oak_planks", ct);

        // Walls: hollow box, 4 blocks tall
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z} {x + 6} {y + 4} {z + 6} minecraft:oak_planks hollow", ct);

        // Peaked roof (oak stairs forming an A-frame along X axis)
        await rcon.SendCommandAsync(
            $"fill {x} {y + 5} {z} {x + 6} {y + 5} {z + 1} minecraft:oak_stairs[facing=south,half=bottom]", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 5} {z + 5} {x + 6} {y + 5} {z + 6} minecraft:oak_stairs[facing=north,half=bottom]", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 5} {z + 2} {x + 6} {y + 5} {z + 4} minecraft:oak_planks", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 6} {z + 3} {x + 6} {y + 6} {z + 3} minecraft:oak_slab", ct);

        // Door - 2 blocks wide
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y + 1} {z} {x + 3} {y + 2} {z} minecraft:air", ct);

        // Cyan stained glass windows
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 3} {z} minecraft:cyan_stained_glass", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 5} {y + 3} {z} minecraft:cyan_stained_glass", ct);
        await rcon.SendCommandAsync(
            $"setblock {x} {y + 3} {z + 3} minecraft:cyan_stained_glass", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 6} {y + 3} {z + 3} minecraft:cyan_stained_glass", ct);

        // Chimney (cobblestone column on one corner)
        await rcon.SendCommandAsync(
            $"fill {x + 6} {y + 5} {z + 6} {x + 6} {y + 7} {z + 6} minecraft:cobblestone", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 6} {y + 7} {z + 6} minecraft:campfire", ct);

        // Interior: crafting table + anvil
        await rcon.SendCommandAsync(
            $"setblock {x + 2} {y + 1} {z + 5} minecraft:crafting_table", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 4} {y + 1} {z + 5} minecraft:anvil", ct);
    }

    /// <summary>
    /// Cottage — humble default dwelling. Unknown/Other resource types.
    /// Cobblestone walls, light blue wool trim, ~7×7 footprint, 5 blocks tall.
    /// </summary>
    private async Task BuildCottageAsync(int x, int y, int z, CancellationToken ct)
    {
        // Foundation: 7×7 cobblestone floor
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z} {x + 6} {y} {z + 6} minecraft:cobblestone", ct);

        // Walls: hollow box, 4 blocks tall
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z} {x + 6} {y + 4} {z + 6} minecraft:cobblestone hollow", ct);

        // Light blue wool trim at top of walls
        await rcon.SendCommandAsync(
            $"fill {x} {y + 4} {z} {x + 6} {y + 4} {z} minecraft:light_blue_wool", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 4} {z + 6} {x + 6} {y + 4} {z + 6} minecraft:light_blue_wool", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 4} {z + 1} {x} {y + 4} {z + 5} minecraft:light_blue_wool", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 6} {y + 4} {z + 1} {x + 6} {y + 4} {z + 5} minecraft:light_blue_wool", ct);

        // Flat roof with slab
        await rcon.SendCommandAsync(
            $"fill {x} {y + 5} {z} {x + 6} {y + 5} {z + 6} minecraft:cobblestone_slab", ct);

        // Door - 2 blocks wide
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y + 1} {z} {x + 3} {y + 2} {z} minecraft:air", ct);

        // Windows (glass panes on sides)
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 2} {z} minecraft:glass_pane", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 5} {y + 2} {z} minecraft:glass_pane", ct);
        await rcon.SendCommandAsync(
            $"setblock {x} {y + 2} {z + 3} minecraft:glass_pane", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 6} {y + 2} {z + 3} minecraft:glass_pane", ct);
    }

    /// <summary>
    /// Places a redstone lamp in the front wall as health indicator.
    /// Healthy = glowstone (always lit), Unhealthy = redstone lamp (unlit), Unknown = air.
    /// </summary>
    /// <summary>
    /// Places a health indicator lamp in the front wall, adapting position to structure type.
    /// Healthy = glowstone (always lit), Unhealthy = redstone lamp (unlit), Unknown = sea lantern.
    /// </summary>
    private async Task PlaceHealthIndicatorAsync(int x, int y, int z, string structureType, ResourceStatus status, CancellationToken ct)
    {
        var lampBlock = status switch
        {
            ResourceStatus.Healthy => "minecraft:glowstone",
            ResourceStatus.Unhealthy => "minecraft:redstone_lamp",
            _ => "minecraft:sea_lantern"
        };

        // Watchtower has front wall at z+1 (hollow 5x5 inside 7x7), others have front wall at z
        var lampZ = structureType == "Watchtower" ? z + 1 : z;
        
        // Place in front wall at y+3 (window/door height), centered above entrance
        await rcon.SendCommandAsync(
            $"setblock {x + 3} {y + 3} {lampZ} {lampBlock}", ct);
    }

    /// <summary>
    /// Places a sign in front of the structure with the resource name and status.
    /// Sign is placed offset from the door entrance (at x+2 instead of x+3).
    /// </summary>
    private async Task PlaceSignAsync(int x, int y, int z, ResourceInfo info, CancellationToken ct)
    {
        var signY = y + 1;
        var signZ = z - 1;

        await rcon.SendCommandAsync(
            $"setblock {x + 2} {signY} {signZ} minecraft:oak_sign[rotation=8]", ct);

        var signCmd = "data merge block " + $"{x + 2} {signY} {signZ}" +
            " {front_text:{messages:[\"\"," +
            "\"" + info.Name + "\"," +
            "\"(" + info.Status + ")\"," +
            "\"\"]}}";
        await rcon.SendCommandAsync(signCmd, ct);
    }
}
