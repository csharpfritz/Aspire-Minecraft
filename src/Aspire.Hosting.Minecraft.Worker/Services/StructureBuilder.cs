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
    private bool _initialBuildComplete;
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

            // After the first complete build, flush world to disk and trigger BlueMap re-render
            if (!_initialBuildComplete && _fenceBuilt && _pathsBuilt && _builtStructures.Count == monitor.TotalCount)
            {
                _initialBuildComplete = true;
                await TriggerBlueMapUpdateAsync(ct);
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
    /// Saves the world to disk and tells BlueMap to re-render the village area.
    /// Called once after the initial village build so the map shows the structures.
    /// </summary>
    private async Task TriggerBlueMapUpdateAsync(CancellationToken ct)
    {
        try
        {
            await rcon.SendCommandAsync("save-all", ct);
            // Small delay to let the server flush chunks to disk
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            await rcon.SendCommandAsync("bluemap update", ct);
            logger.LogInformation("BlueMap render update triggered after village build");
        }
        catch (Exception ex)
        {
            // BlueMap may not be installed — don't fail the build
            logger.LogDebug(ex, "BlueMap update command failed (plugin may not be installed)");
        }
    }

    /// <summary>
    /// Returns the wool, banner, and wall banner block IDs for a resource based on its type and name.
    /// .NET Projects = purple, JavaScript/Node = yellow, Python = blue, Go = cyan,
    /// Java = orange, Rust = brown, default/unknown = white.
    /// </summary>
    internal static (string wool, string banner, string wallBanner) GetLanguageColor(string resourceType, string resourceName)
    {
        if (resourceType.Equals("Project", StringComparison.OrdinalIgnoreCase))
            return ("minecraft:purple_wool", "minecraft:purple_banner", "minecraft:purple_wall_banner");

        var combined = (resourceType + " " + resourceName).ToLowerInvariant();

        if (combined.Contains("node") || combined.Contains("javascript") || combined.Contains("js"))
            return ("minecraft:yellow_wool", "minecraft:yellow_banner", "minecraft:yellow_wall_banner");
        if (combined.Contains("python") || combined.Contains("flask") || combined.Contains("django"))
            return ("minecraft:blue_wool", "minecraft:blue_banner", "minecraft:blue_wall_banner");
        if (combined.Contains("go") || combined.Contains("golang"))
            return ("minecraft:cyan_wool", "minecraft:cyan_banner", "minecraft:cyan_wall_banner");
        if (combined.Contains("java") || combined.Contains("spring"))
            return ("minecraft:orange_wool", "minecraft:orange_banner", "minecraft:orange_wall_banner");
        if (combined.Contains("rust"))
            return ("minecraft:brown_wool", "minecraft:brown_banner", "minecraft:brown_wall_banner");

        return ("minecraft:white_wool", "minecraft:white_banner", "minecraft:white_wall_banner");
    }

    /// <summary>
    /// Determines if a resource type represents a database or data store.
    /// </summary>
    internal static bool IsDatabaseResource(string resourceType)
    {
        var lower = resourceType.ToLowerInvariant();
        return lower.Contains("postgres")
            || lower.Contains("redis")
            || lower.Contains("sqlserver")
            || lower.Contains("sql-server")
            || lower.Contains("mongodb")
            || lower.Contains("mysql")
            || lower.Contains("mariadb")
            || lower.Contains("cosmosdb")
            || lower.Contains("oracle")
            || lower.Contains("sqlite")
            || lower.Contains("rabbitmq");
    }

    /// <summary>
    /// Determines if a resource type represents an Azure-hosted resource.
    /// </summary>
    internal static bool IsAzureResource(string resourceType)
    {
        var lower = resourceType.ToLowerInvariant();
        return lower.Contains("azure")
            || lower.Contains("cosmos")
            || lower.Contains("servicebus")
            || lower.Contains("eventhub")
            || lower.Contains("keyvault")
            || lower.Contains("appconfiguration")
            || lower.Contains("signalr")
            || lower.Contains("storage");
    }

    /// <summary>
    /// Selects the structure type based on Aspire resource type.
    /// Database resources get Cylinder, Azure (non-database) resources get AzureThemed,
    /// then falls through to standard type mapping.
    /// </summary>
    internal static string GetStructureType(string resourceType)
    {
        if (IsDatabaseResource(resourceType))
            return "Cylinder";

        if (IsAzureResource(resourceType))
            return "AzureThemed";

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
            var fenceY = VillageLayout.SurfaceY + 1;

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

            // Step 1: Clear blocks above the surface (where structure floors now sit)
            await rcon.SendCommandAsync(
                $"fill {fMinX + 1} {VillageLayout.SurfaceY + 1} {fMinZ + 1} {fMaxX - 1} {VillageLayout.SurfaceY + 1} {fMaxZ - 1} minecraft:air", ct);

            // Step 2: Place cobblestone at SurfaceY (the grass surface level, one below new floor level)
            await rcon.SendCommandAsync(
                $"fill {fMinX + 1} {VillageLayout.SurfaceY} {fMinZ + 1} {fMaxX - 1} {VillageLayout.SurfaceY} {fMaxZ - 1} minecraft:cobblestone", ct);

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
                    await BuildWatchtowerAsync(x, y, z, info, ct);
                    break;
                case "Warehouse":
                    await BuildWarehouseAsync(x, y, z, ct);
                    break;
                case "Workshop":
                    await BuildWorkshopAsync(x, y, z, ct);
                    break;
                case "Cylinder":
                    await BuildCylinderAsync(x, y, z, ct);
                    break;
                case "AzureThemed":
                    await BuildAzureThemedAsync(x, y, z, info, ct);
                    break;
                default:
                    await BuildCottageAsync(x, y, z, info, ct);
                    break;
            }

            // Azure resources get a light blue banner on the rooftop
            if (IsAzureResource(info.Type))
            {
                await PlaceAzureBannerAsync(x, y, z, structureType, ct);
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
    /// Stone brick walls, language-colored wool/banner accents, ~7×7 footprint, 10 blocks tall.
    /// </summary>
    private async Task BuildWatchtowerAsync(int x, int y, int z, ResourceInfo info, CancellationToken ct)
    {
        var (wool, banner, _) = GetLanguageColor(info.Type, info.Name);

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

        // Language-colored wool trim at top
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 8} {z + 1} {x + 5} {y + 8} {z + 5} {wool}", ct);

        // Roof cap (3×3 stone brick slab)
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y + 9} {z + 2} {x + 4} {y + 9} {z + 4} minecraft:stone_brick_slab", ct);

        // Flag pole + standing banner on top
        await rcon.SendCommandAsync(
            $"fill {x + 3} {y + 9} {z + 3} {x + 3} {y + 10} {z + 3} minecraft:oak_fence", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 3} {y + 11} {z + 3} {banner}[rotation=0]", ct);

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
    /// Standard (7×7): Cobblestone walls, language-colored wool trim, 5 blocks tall.
    /// Grand (15×15): 8 blocks tall, cobblestone lower / oak plank upper walls,
    /// language-colored wool trim at roof level, cobblestone slab pitched roof,
    /// flower pots and window boxes on front face, furnished interior.
    /// </summary>
    private async Task BuildCottageAsync(int x, int y, int z, ResourceInfo info, CancellationToken ct)
    {
        if (VillageLayout.StructureSize == 15)
        {
            await BuildGrandCottageAsync(x, y, z, info, ct);
            return;
        }

        var (wool, _, _) = GetLanguageColor(info.Type, info.Name);

        // Foundation: 7×7 cobblestone floor
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z} {x + 6} {y} {z + 6} minecraft:cobblestone", ct);

        // Walls: hollow box, 4 blocks tall
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z} {x + 6} {y + 4} {z + 6} minecraft:cobblestone hollow", ct);

        // Language-colored wool trim at top of walls
        await rcon.SendCommandAsync(
            $"fill {x} {y + 4} {z} {x + 6} {y + 4} {z} {wool}", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 4} {z + 6} {x + 6} {y + 4} {z + 6} {wool}", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 4} {z + 1} {x} {y + 4} {z + 5} {wool}", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 6} {y + 4} {z + 1} {x + 6} {y + 4} {z + 5} {wool}", ct);

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
    /// Grand Cottage — 15×15 footprint, 8 blocks tall.
    /// Cobblestone lower walls (y+1 to y+4), oak plank upper walls (y+5 to y+7).
    /// Language-colored wool trim band at roof level (y+7).
    /// Cobblestone slab pitched roof. Flower pots and window boxes on front face.
    /// Interior: bed, crafting table, bookshelf, furnace, 2 chests, potted flowers, 4 torches.
    /// ~40-50 RCON commands.
    /// </summary>
    private async Task BuildGrandCottageAsync(int x, int y, int z, ResourceInfo info, CancellationToken ct)
    {
        var (wool, _, _) = GetLanguageColor(info.Type, info.Name);
        var s = VillageLayout.StructureSize - 1; // 14
        var half = s / 2; // 7

        // === FOUNDATION: 15×15 cobblestone floor ===
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z} {x + s} {y} {z + s} minecraft:cobblestone", ct);

        // === LOWER WALLS: cobblestone, y+1 to y+4 ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z} {x + s} {y + 4} {z + s} minecraft:cobblestone hollow", ct);

        // === UPPER WALLS: oak planks, y+5 to y+7 ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 5} {z} {x + s} {y + 7} {z + s} minecraft:oak_planks hollow", ct);

        // === LANGUAGE-COLORED WOOL TRIM at roof level (y+7) ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 7} {z} {x + s} {y + 7} {z} {wool}", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 7} {z + s} {x + s} {y + 7} {z + s} {wool}", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 7} {z + 1} {x} {y + 7} {z + s - 1} {wool}", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 7} {z + 1} {x + s} {y + 7} {z + s - 1} {wool}", ct);

        // === PITCHED ROOF: cobblestone slabs ===
        // South slope
        await rcon.SendCommandAsync(
            $"fill {x} {y + 8} {z} {x + s} {y + 8} {z + 3} minecraft:cobblestone_slab", ct);
        // North slope
        await rcon.SendCommandAsync(
            $"fill {x} {y + 8} {z + s - 3} {x + s} {y + 8} {z + s} minecraft:cobblestone_slab", ct);
        // Ridge (center)
        await rcon.SendCommandAsync(
            $"fill {x} {y + 8} {z + 4} {x + s} {y + 8} {z + s - 4} minecraft:cobblestone_slab", ct);

        // === DOOR: 2 blocks wide, 2 tall on front face (z-min) ===
        await rcon.SendCommandAsync(
            $"fill {x + half - 1} {y + 1} {z} {x + half} {y + 2} {z} minecraft:air", ct);

        // === FRONT FACE WINDOWS: glass panes flanking door ===
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y + 3} {z} {x + 4} {y + 4} {z} minecraft:glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 10} {y + 3} {z} {x + 12} {y + 4} {z} minecraft:glass_pane", ct);

        // === SIDE WINDOWS ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 3} {z + 4} {x} {y + 4} {z + 6} minecraft:glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 3} {z + 4} {x + s} {y + 4} {z + 6} minecraft:glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 3} {z + 8} {x} {y + 4} {z + 10} minecraft:glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 3} {z + 8} {x + s} {y + 4} {z + 10} minecraft:glass_pane", ct);

        // === FLOWER POTS on front face window ledges ===
        await rcon.SendCommandAsync(
            $"setblock {x + 3} {y + 2} {z - 1} minecraft:flower_pot", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 11} {y + 2} {z - 1} minecraft:flower_pot", ct);

        // === INTERIOR: bed ===
        await rcon.SendCommandAsync(
            $"setblock {x + 2} {y + 1} {z + s - 2} minecraft:red_bed[facing=south,part=head]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 2} {y + 1} {z + s - 1} minecraft:red_bed[facing=south,part=foot]", ct);

        // === INTERIOR: crafting table ===
        await rcon.SendCommandAsync(
            $"setblock {x + 4} {y + 1} {z + s - 2} minecraft:crafting_table", ct);

        // === INTERIOR: bookshelf ===
        await rcon.SendCommandAsync(
            $"setblock {x + 6} {y + 1} {z + s - 1} minecraft:bookshelf", ct);

        // === INTERIOR: furnace ===
        await rcon.SendCommandAsync(
            $"setblock {x + s - 2} {y + 1} {z + s - 1} minecraft:furnace[facing=north]", ct);

        // === INTERIOR: 2 chests ===
        await rcon.SendCommandAsync(
            $"setblock {x + s - 2} {y + 1} {z + 2} minecraft:chest[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 3} {y + 1} {z + 2} minecraft:chest[facing=south]", ct);

        // === INTERIOR: potted flowers ===
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 1} {z + 1} minecraft:potted_poppy", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 1} {z + 1} minecraft:potted_dandelion", ct);

        // === INTERIOR: 4 torches for lighting ===
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 3} {z + 1} minecraft:wall_torch[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 3} {z + 1} minecraft:wall_torch[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 3} {z + s - 1} minecraft:wall_torch[facing=north]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 3} {z + s - 1} minecraft:wall_torch[facing=north]", ct);

        logger.LogInformation("Grand Cottage built at ({X},{Y},{Z})", x, y, z);
    }

    /// <summary>
    /// Cylinder — round building evoking the database cylinder icon. Database resources.
    /// Smooth stone walls, polished deepslate floor and top band, dome roof.
    /// Radius 3 = 7-block diameter, fits in the existing 7×7 grid cell.
    /// </summary>
    private async Task BuildCylinderAsync(int x, int y, int z, CancellationToken ct)
    {
        // === FLOOR (y+0): polished deepslate disc ===
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y} {z} {x + 4} {y} {z} minecraft:polished_deepslate", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y} {z + 1} {x + 5} {y} {z + 1} minecraft:polished_deepslate", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z + 2} {x + 6} {y} {z + 2} minecraft:polished_deepslate", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z + 3} {x + 6} {y} {z + 3} minecraft:polished_deepslate", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z + 4} {x + 6} {y} {z + 4} minecraft:polished_deepslate", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y} {z + 5} {x + 5} {y} {z + 5} minecraft:polished_deepslate", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y} {z + 6} {x + 4} {y} {z + 6} minecraft:polished_deepslate", ct);

        // === WALLS (y+1 to y+4): smooth_stone perimeter, polished_deepslate top band ===
        for (int layer = 1; layer <= 4; layer++)
        {
            string wallBlock = layer <= 3
                ? "minecraft:smooth_stone"
                : "minecraft:polished_deepslate";

            // North face (z+0): 3 blocks
            await rcon.SendCommandAsync(
                $"fill {x + 2} {y + layer} {z} {x + 4} {y + layer} {z} {wallBlock}", ct);
            // South face (z+6): 3 blocks
            await rcon.SendCommandAsync(
                $"fill {x + 2} {y + layer} {z + 6} {x + 4} {y + layer} {z + 6} {wallBlock}", ct);
            // Upper diagonals
            await rcon.SendCommandAsync(
                $"setblock {x + 1} {y + layer} {z + 1} {wallBlock}", ct);
            await rcon.SendCommandAsync(
                $"setblock {x + 5} {y + layer} {z + 1} {wallBlock}", ct);
            // Lower diagonals
            await rcon.SendCommandAsync(
                $"setblock {x + 1} {y + layer} {z + 5} {wallBlock}", ct);
            await rcon.SendCommandAsync(
                $"setblock {x + 5} {y + layer} {z + 5} {wallBlock}", ct);
            // East/west faces (z+2 to z+4)
            await rcon.SendCommandAsync(
                $"fill {x} {y + layer} {z + 2} {x} {y + layer} {z + 4} {wallBlock}", ct);
            await rcon.SendCommandAsync(
                $"fill {x + 6} {y + layer} {z + 2} {x + 6} {y + layer} {z + 4} {wallBlock}", ct);
        }

        // === INTERIOR AIR (y+1 to y+4): clear inside the cylinder ===
        for (int layer = 1; layer <= 4; layer++)
        {
            await rcon.SendCommandAsync(
                $"fill {x + 2} {y + layer} {z + 1} {x + 4} {y + layer} {z + 1} minecraft:air", ct);
            await rcon.SendCommandAsync(
                $"fill {x + 1} {y + layer} {z + 2} {x + 5} {y + layer} {z + 2} minecraft:air", ct);
            await rcon.SendCommandAsync(
                $"fill {x + 1} {y + layer} {z + 3} {x + 5} {y + layer} {z + 3} minecraft:air", ct);
            await rcon.SendCommandAsync(
                $"fill {x + 1} {y + layer} {z + 4} {x + 5} {y + layer} {z + 4} minecraft:air", ct);
            await rcon.SendCommandAsync(
                $"fill {x + 2} {y + layer} {z + 5} {x + 4} {y + layer} {z + 5} minecraft:air", ct);
        }

        // === DOME ROOF (y+5): smooth_stone_slab outer ring ===
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y + 5} {z} {x + 4} {y + 5} {z} minecraft:smooth_stone_slab", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 5} {z + 1} {x + 5} {y + 5} {z + 1} minecraft:smooth_stone_slab", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 5} {z + 2} {x + 6} {y + 5} {z + 2} minecraft:smooth_stone_slab", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 5} {z + 3} {x + 6} {y + 5} {z + 3} minecraft:smooth_stone_slab", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 5} {z + 4} {x + 6} {y + 5} {z + 4} minecraft:smooth_stone_slab", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 5} {z + 5} {x + 5} {y + 5} {z + 5} minecraft:smooth_stone_slab", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y + 5} {z + 6} {x + 4} {y + 5} {z + 6} minecraft:smooth_stone_slab", ct);

        // === DOME CAP (y+6): polished_deepslate_slab inner cap ===
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y + 6} {z + 1} {x + 4} {y + 6} {z + 1} minecraft:polished_deepslate_slab", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 6} {z + 2} {x + 5} {y + 6} {z + 2} minecraft:polished_deepslate_slab", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 6} {z + 3} {x + 5} {y + 6} {z + 3} minecraft:polished_deepslate_slab", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 6} {z + 4} {x + 5} {y + 6} {z + 4} minecraft:polished_deepslate_slab", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y + 6} {z + 5} {x + 4} {y + 6} {z + 5} minecraft:polished_deepslate_slab", ct);

        // === DOOR (south face, z+0): 1-wide centered at x+3, 2-tall ===
        await rcon.SendCommandAsync(
            $"fill {x + 3} {y + 1} {z} {x + 3} {y + 2} {z} minecraft:air", ct);

        // === INTERIOR ACCENTS ===
        await rcon.SendCommandAsync(
            $"setblock {x + 3} {y} {z + 3} minecraft:copper_block", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 2} {y} {z + 3} minecraft:copper_block", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 4} {y} {z + 3} minecraft:copper_block", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 1} {z + 1} minecraft:iron_block", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 5} {y + 1} {z + 1} minecraft:iron_block", ct);
    }

    /// <summary>
    /// AzureThemed — modified Cottage with Azure color palette. Non-database Azure resources.
    /// Standard (7×7): Light blue concrete walls, blue concrete trim, stained glass roof.
    /// Grand (15×15): 8 blocks tall, pilaster strips, 3×3 skylight, banners on all 4 roof corners,
    /// light blue carpet floor, blue stained glass internal windows, brewing stand and cauldron.
    /// </summary>
    private async Task BuildAzureThemedAsync(int x, int y, int z, ResourceInfo info, CancellationToken ct)
    {
        if (VillageLayout.StructureSize == 15)
        {
            await BuildGrandAzurePavilionAsync(x, y, z, info, ct);
            return;
        }

        // Foundation: 7×7 light blue concrete floor
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z} {x + 6} {y} {z + 6} minecraft:light_blue_concrete", ct);

        // Walls: hollow box, 4 blocks tall
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z} {x + 6} {y + 4} {z + 6} minecraft:light_blue_concrete hollow", ct);

        // Blue concrete trim at top of walls
        await rcon.SendCommandAsync(
            $"fill {x} {y + 4} {z} {x + 6} {y + 4} {z} minecraft:blue_concrete", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 4} {z + 6} {x + 6} {y + 4} {z + 6} minecraft:blue_concrete", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 4} {z + 1} {x} {y + 4} {z + 5} minecraft:blue_concrete", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 6} {y + 4} {z + 1} {x + 6} {y + 4} {z + 5} minecraft:blue_concrete", ct);

        // Flat roof with light blue stained glass
        await rcon.SendCommandAsync(
            $"fill {x} {y + 5} {z} {x + 6} {y + 5} {z + 6} minecraft:light_blue_stained_glass", ct);

        // Door - 2 blocks wide
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y + 1} {z} {x + 3} {y + 2} {z} minecraft:air", ct);

        // Windows (blue stained glass panes on sides)
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 2} {z} minecraft:blue_stained_glass_pane", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 5} {y + 2} {z} minecraft:blue_stained_glass_pane", ct);
        await rcon.SendCommandAsync(
            $"setblock {x} {y + 2} {z + 3} minecraft:blue_stained_glass_pane", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 6} {y + 2} {z + 3} minecraft:blue_stained_glass_pane", ct);

        // Azure banner on roof (always for AzureThemed)
        await rcon.SendCommandAsync(
            $"setblock {x + 3} {y + 6} {z + 3} minecraft:light_blue_banner[rotation=0]", ct);
    }

    /// <summary>
    /// Grand Azure Pavilion — 15×15 footprint, 8 blocks tall.
    /// Light blue concrete walls with blue concrete pilaster strips at corners and midpoints.
    /// Flat roof with light blue stained glass skylight (3×3 center).
    /// Azure banners on all four roof corners.
    /// Interior: light blue carpet floor, blue stained glass internal windows,
    /// brewing stand and cauldron (cloud services aesthetic).
    /// ~50-60 RCON commands.
    /// </summary>
    private async Task BuildGrandAzurePavilionAsync(int x, int y, int z, ResourceInfo info, CancellationToken ct)
    {
        var s = VillageLayout.StructureSize - 1; // 14
        var half = s / 2; // 7

        // === FOUNDATION: 15×15 light blue concrete floor ===
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z} {x + s} {y} {z + s} minecraft:light_blue_concrete", ct);

        // === WALLS: light blue concrete, 7 blocks tall (y+1 to y+7) ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z} {x + s} {y + 7} {z + s} minecraft:light_blue_concrete hollow", ct);

        // === BLUE CONCRETE PILASTER STRIPS at corners and midpoints (full height) ===
        // Corners
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z} {x} {y + 7} {z} minecraft:blue_concrete", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 1} {z} {x + s} {y + 7} {z} minecraft:blue_concrete", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z + s} {x} {y + 7} {z + s} minecraft:blue_concrete", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 1} {z + s} {x + s} {y + 7} {z + s} minecraft:blue_concrete", ct);
        // Midpoints (center of each wall)
        await rcon.SendCommandAsync(
            $"fill {x + half} {y + 1} {z} {x + half} {y + 7} {z} minecraft:blue_concrete", ct);
        await rcon.SendCommandAsync(
            $"fill {x + half} {y + 1} {z + s} {x + half} {y + 7} {z + s} minecraft:blue_concrete", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z + half} {x} {y + 7} {z + half} minecraft:blue_concrete", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 1} {z + half} {x + s} {y + 7} {z + half} minecraft:blue_concrete", ct);

        // === BLUE CONCRETE TRIM BAND at wall top (y+7) ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 7} {z} {x + s} {y + 7} {z} minecraft:blue_concrete", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 7} {z + s} {x + s} {y + 7} {z + s} minecraft:blue_concrete", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 7} {z + 1} {x} {y + 7} {z + s - 1} minecraft:blue_concrete", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 7} {z + 1} {x + s} {y + 7} {z + s - 1} minecraft:blue_concrete", ct);

        // === FLAT ROOF: light blue concrete (y+8) ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 8} {z} {x + s} {y + 8} {z + s} minecraft:light_blue_concrete", ct);

        // === SKYLIGHT: 3×3 light blue stained glass in center of roof ===
        await rcon.SendCommandAsync(
            $"fill {x + half - 1} {y + 8} {z + half - 1} {x + half + 1} {y + 8} {z + half + 1} minecraft:light_blue_stained_glass", ct);

        // === AZURE BANNERS on all four roof corners ===
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 9} {z + 1} minecraft:light_blue_banner[rotation=0]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 9} {z + 1} minecraft:light_blue_banner[rotation=0]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 9} {z + s - 1} minecraft:light_blue_banner[rotation=0]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 9} {z + s - 1} minecraft:light_blue_banner[rotation=0]", ct);

        // === DOOR: 2 blocks wide, 2 tall on front face (z-min) ===
        await rcon.SendCommandAsync(
            $"fill {x + half - 1} {y + 1} {z} {x + half} {y + 2} {z} minecraft:air", ct);

        // === BLUE STAINED GLASS INTERNAL WINDOWS on side walls (y+3 to y+5) ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 3} {z + 3} {x} {y + 5} {z + 5} minecraft:blue_stained_glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 3} {z + 3} {x + s} {y + 5} {z + 5} minecraft:blue_stained_glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 3} {z + 9} {x} {y + 5} {z + 11} minecraft:blue_stained_glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 3} {z + 9} {x + s} {y + 5} {z + 11} minecraft:blue_stained_glass_pane", ct);
        // Front windows (flanking door)
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y + 3} {z} {x + 4} {y + 5} {z} minecraft:blue_stained_glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 10} {y + 3} {z} {x + 12} {y + 5} {z} minecraft:blue_stained_glass_pane", ct);
        // Back windows
        await rcon.SendCommandAsync(
            $"fill {x + 3} {y + 3} {z + s} {x + 5} {y + 5} {z + s} minecraft:blue_stained_glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 9} {y + 3} {z + s} {x + 11} {y + 5} {z + s} minecraft:blue_stained_glass_pane", ct);

        // === INTERIOR: light blue carpet floor ===
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 1} {z + 1} {x + s - 1} {y + 1} {z + s - 1} minecraft:light_blue_carpet", ct);

        // === INTERIOR: brewing stand and cauldron (cloud services aesthetic) ===
        await rcon.SendCommandAsync(
            $"setblock {x + 3} {y + 1} {z + s - 2} minecraft:brewing_stand", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 5} {y + 1} {z + s - 2} minecraft:cauldron", ct);

        // === INTERIOR: lanterns for lighting ===
        await rcon.SendCommandAsync(
            $"setblock {x + 4} {y + 7} {z + 4} minecraft:lantern[hanging=true]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 10} {y + 7} {z + 4} minecraft:lantern[hanging=true]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 4} {y + 7} {z + 10} minecraft:lantern[hanging=true]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 10} {y + 7} {z + 10} minecraft:lantern[hanging=true]", ct);

        logger.LogInformation("Grand Azure Pavilion built at ({X},{Y},{Z})", x, y, z);
    }

    /// <summary>
    /// Places an Azure light blue banner on the rooftop of any structure type.
    /// Uses a flagpole (oak fence) with the banner beside it.
    /// </summary>
    private async Task PlaceAzureBannerAsync(int x, int y, int z, string structureType, CancellationToken ct)
    {
        // AzureThemed already places its banner in BuildAzureThemedAsync
        if (structureType == "AzureThemed")
            return;

        var isGrand = VillageLayout.StructureSize >= 15;
        var half = VillageLayout.StructureSize / 2;

        int roofY = structureType switch
        {
            "Watchtower" => y + 9,
            "Warehouse" => y + 6,
            "Workshop" when isGrand => y + 10,
            "Workshop" => y + 7,
            "Cylinder" => y + 7,
            "Cottage" => isGrand ? y + 9 : y + 6,
            _ => isGrand ? y + 9 : y + 6,
        };

        // Flagpole
        await rcon.SendCommandAsync(
            $"fill {x + half} {roofY} {z + half} {x + half} {roofY + 1} {z + half} minecraft:oak_fence", ct);

        // Azure standing banner on top of flagpole
        await rcon.SendCommandAsync(
            $"setblock {x + half} {roofY + 2} {z + half} minecraft:light_blue_banner[rotation=0]", ct);
    }

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

        var isGrand = VillageLayout.StructureSize >= 15;

        // Watchtower has front wall at z+1 (hollow 5x5 inside 7x7), others have front wall at z
        var lampZ = structureType == "Watchtower" ? z + 1 : z;
        
        // Grand Workshop has 3-tall door (y+1 to y+3), so lamp at y+4.
        // Watchtower and Warehouse have 3-tall doors (y+1 to y+3), so lamp goes at y+4 to avoid overlap.
        // Workshop, Cottage, Cylinder, and AzureThemed have 2-tall doors (y+1 to y+2), so y+3 is fine.
        var lampY = structureType switch
        {
            "Watchtower" or "Warehouse" => y + 4,
            "Workshop" when isGrand => y + 4,
            "Cylinder" or "AzureThemed" or "Workshop" or "Cottage" or _ => y + 3
        };

        // Center X adapts to structure size for grand variants
        var half = VillageLayout.StructureSize / 2;
        var lampX = isGrand ? x + half : x + 3;
        
        // Place in front wall centered above entrance
        await rcon.SendCommandAsync(
            $"setblock {lampX} {lampY} {lampZ} {lampBlock}", ct);
    }

    /// <summary>
    /// Places a sign in front of the structure with the resource name and status.
    /// Sign is placed offset from the door entrance (at x+2 instead of x+3).
    /// </summary>
    private async Task PlaceSignAsync(int x, int y, int z, ResourceInfo info, CancellationToken ct)
    {
        var half = VillageLayout.StructureSize / 2;
        var signX = x + half - 1;
        var signY = y + 1;
        var signZ = z - 1;

        await rcon.SendCommandAsync(
            $"setblock {signX} {signY} {signZ} minecraft:oak_sign[rotation=8]", ct);

        var signCmd = "data merge block " + $"{signX} {signY} {signZ}" +
            " {front_text:{messages:[\"\"," +
            "\"" + info.Name + "\"," +
            "\"(" + info.Status + ")\"," +
            "\"\"]}}";
        await rcon.SendCommandAsync(signCmd, ct);
    }
}
