using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Describes the entrance to a structure: where the door is, and derived positions
/// for the health indicator glow block and resource name sign.
/// </summary>
/// <param name="CenterX">X coordinate at the center of the door opening.</param>
/// <param name="TopY">Y coordinate of the topmost block of the door opening.</param>
/// <param name="FaceZ">Z coordinate of the wall face containing the door.</param>
internal readonly record struct DoorPosition(int CenterX, int TopY, int FaceZ)
{
    /// <summary>The glow block goes directly above the door, centered, flush with the wall.</summary>
    public (int X, int Y, int Z) GlowBlock => (CenterX, TopY + 1, FaceZ);

    /// <summary>The sign goes on the front wall face, offset two blocks to the left of the door center.</summary>
    public (int X, int Y, int Z) Sign => (CenterX - 2, TopY - 1, FaceZ);
}

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
    private readonly Dictionary<string, DoorPosition> _doorPositions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the door position for a resource, if the structure has been built.
    /// Used by other services (e.g., ServiceSwitchService) to place elements relative to the door.
    /// </summary>
    public bool TryGetDoorPosition(string resourceName, out DoorPosition door)
        => _doorPositions.TryGetValue(resourceName, out door);

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
                        if (_doorPositions.TryGetValue(name, out var doorPos))
                        {
                            await PlaceHealthIndicatorAsync(doorPos, info.Status, ct);
                        }
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
            var gateWidth = VillageLayout.GateWidth;

            // South side (low Z) — two segments with a gate gap in the center
            var gateX = VillageLayout.BaseX + VillageLayout.StructureSize; // center of the boulevard
            await rcon.SendCommandAsync(
                $"fill {fMinX} {fenceY} {fMinZ} {gateX - 1} {fenceY} {fMinZ} minecraft:oak_fence", ct);
            await rcon.SendCommandAsync(
                $"fill {gateX + gateWidth - 1} {fenceY} {fMinZ} {fMaxX} {fenceY} {fMinZ} minecraft:oak_fence", ct);
            // Gate (width adapts to layout: 3 for standard, 5 for grand)
            await rcon.SendCommandAsync(
                $"fill {gateX} {fenceY} {fMinZ} {gateX + gateWidth - 1} {fenceY} {fMinZ} minecraft:oak_fence_gate[facing=south]", ct);

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

            // Step 3: For Grand layout, add a central boulevard (3-wide stone brick) between columns
            if (VillageLayout.IsGrandLayout && rows > 0)
            {
                var boulevardX = VillageLayout.BaseX + VillageLayout.StructureSize; // between column 0 and column 1
                await rcon.SendCommandAsync(
                    $"fill {boulevardX} {VillageLayout.SurfaceY} {fMinZ + 1} {boulevardX + 2} {VillageLayout.SurfaceY} {fMaxZ - 1} minecraft:stone_bricks", ct);
            }

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

            // Each builder returns its door position — single source of truth
            DoorPosition door = structureType switch
            {
                "Watchtower" => await BuildWatchtowerAsync(x, y, z, info, ct),
                "Warehouse" => await BuildWarehouseAsync(x, y, z, info, ct),
                "Workshop" => await BuildWorkshopAsync(x, y, z, ct),
                "Cylinder" => await BuildCylinderAsync(x, y, z, ct),
                "AzureThemed" => await BuildAzureThemedAsync(x, y, z, info, ct),
                _ => await BuildCottageAsync(x, y, z, info, ct),
            };

            _doorPositions[info.Name] = door;

            // Azure resources get a light blue banner on the rooftop
            if (IsAzureResource(info.Type))
            {
                await PlaceAzureBannerAsync(x, y, z, structureType, ct);
            }

            // Health indicator glow block — placed directly above the door
            await PlaceHealthIndicatorAsync(door, info.Status, ct);

            // Sign with resource name — placed just outside the door
            await PlaceSignAsync(door, info, ct);

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
    /// Standard: Stone brick walls, language-colored wool/banner accents, ~7×7 footprint, 10 blocks tall.
    /// Grand: 15×15 footprint, 20+ blocks tall, 3-floor interior with spiral staircase,
    /// deepslate brick corner buttresses, crenellated battlements with merlons, arrow slit windows,
    /// taller corner turrets with stair caps and banners, portcullis gatehouse with iron bars,
    /// mossy/cracked weathering, string courses, machicolations, and exterior lanterns.
    /// </summary>
    private async Task<DoorPosition> BuildWatchtowerAsync(int x, int y, int z, ResourceInfo info, CancellationToken ct)
    {
        if (VillageLayout.StructureSize >= 15)
        {
            return await BuildGrandWatchtowerAsync(x, y, z, info, ct);
        }

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

        return new DoorPosition(x + 3, y + 3, z + 1);
    }

    /// <summary>
    /// Grand Watchtower — ornate medieval 15×15 footprint, 20 blocks tall, 3-floor interior.
    /// Mossy stone base plinth at y, walls from y+1, deepslate brick corner buttresses,
    /// weathered lower walls, wool bands at y+6 and y+12, string courses, machicolations.
    /// Clean 3-wide × 4-tall entrance (y+1 to y+4) with decorative arch and keystone.
    /// Interior unchanged: spiral staircase, 3 oak_planks floors, furniture, sign, torches.
    /// 4 standing banners on roof corners. Language-colored wool bands at y+6 and y+12.
    /// </summary>
    private async Task<DoorPosition> BuildGrandWatchtowerAsync(int x, int y, int z, ResourceInfo info, CancellationToken ct)
    {
        var s = VillageLayout.StructureSize - 1; // 14
        var half = VillageLayout.StructureSize / 2; // 7
        var (wool, banner, _) = GetLanguageColor(info.Type, info.Name);

        // === BASE: mossy stone brick plinth ===
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z} {x + s} {y} {z + s} minecraft:mossy_stone_bricks", ct);

        // === MAIN WALLS: stone brick shell (y+1 to y+18) ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z} {x + s} {y + 18} {z + s} minecraft:stone_bricks hollow", ct);

        // === WEATHERING: cracked stone at lower walls for aged look ===
        await rcon.SendCommandAsync(
            $"fill {x + 3} {y + 1} {z} {x + s - 3} {y + 2} {z} minecraft:cracked_stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 3} {y + 1} {z + s} {x + s - 3} {y + 2} {z + s} minecraft:cracked_stone_bricks", ct);

        // === PROTRUDING CORNER BUTTRESSES: deepslate brick pillars rising above the walls ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z} {x + 2} {y + 20} {z + 2} minecraft:deepslate_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s - 2} {y + 1} {z} {x + s} {y + 20} {z + 2} minecraft:deepslate_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z + s - 2} {x + 2} {y + 20} {z + s} minecraft:deepslate_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s - 2} {y + 1} {z + s - 2} {x + s} {y + 20} {z + s} minecraft:deepslate_bricks", ct);

        // === LANGUAGE-COLORED WOOL BANDS at y+6 and y+12 ===
        await rcon.SendCommandAsync(
            $"fill {x + 3} {y + 6} {z} {x + s - 3} {y + 6} {z} {wool}", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 3} {y + 6} {z + s} {x + s - 3} {y + 6} {z + s} {wool}", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 6} {z + 3} {x} {y + 6} {z + s - 3} {wool}", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 6} {z + 3} {x + s} {y + 6} {z + s - 3} {wool}", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 3} {y + 12} {z} {x + s - 3} {y + 12} {z} {wool}", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 3} {y + 12} {z + s} {x + s - 3} {y + 12} {z + s} {wool}", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 12} {z + 3} {x} {y + 12} {z + s - 3} {wool}", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 12} {z + 3} {x + s} {y + 12} {z + s - 3} {wool}", ct);

        // === STRING COURSES: corbel ledges above first wool band (front/back) ===
        await rcon.SendCommandAsync(
            $"fill {x + 3} {y + 7} {z} {x + s - 3} {y + 7} {z} minecraft:stone_brick_stairs[facing=south,half=top]", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 3} {y + 7} {z + s} {x + s - 3} {y + 7} {z + s} minecraft:stone_brick_stairs[facing=north,half=top]", ct);

        // === WINDOW BAYS: iron bar arrow slits on lower floors, glass observation deck ===
        await rcon.SendCommandAsync(
            $"fill {x + 4} {y + 3} {z} {x + 5} {y + 4} {z} minecraft:iron_bars", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 9} {y + 3} {z} {x + 10} {y + 4} {z} minecraft:iron_bars", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 5} {y + 9} {z} {x + 9} {y + 10} {z} minecraft:glass_pane", ct);
        // Observation windows on all sides (3rd floor, 2-high for drama)
        await rcon.SendCommandAsync(
            $"fill {x + 4} {y + 15} {z} {x + 10} {y + 16} {z} minecraft:glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 4} {y + 15} {z + s} {x + 10} {y + 16} {z + s} minecraft:glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 15} {z + 4} {x} {y + 16} {z + 10} minecraft:glass_pane", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 15} {z + 4} {x + s} {y + 16} {z + 10} minecraft:glass_pane", ct);

        // === MACHICOLATIONS: upside-down stairs projecting outward below parapet ===
        await rcon.SendCommandAsync(
            $"fill {x + 3} {y + 19} {z} {x + s - 3} {y + 19} {z} minecraft:stone_brick_stairs[facing=south,half=top]", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 3} {y + 19} {z + s} {x + s - 3} {y + 19} {z + s} minecraft:stone_brick_stairs[facing=north,half=top]", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 19} {z + 3} {x} {y + 19} {z + s - 3} minecraft:stone_brick_stairs[facing=east,half=top]", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 19} {z + 3} {x + s} {y + 19} {z + s - 3} minecraft:stone_brick_stairs[facing=west,half=top]", ct);

        // === BATTLEMENTS: parapet ring with alternating merlons and crenels ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 20} {z} {x + s} {y + 20} {z + s} minecraft:stone_bricks hollow", ct);
        // Merlons: 2-wide stone blocks on front and back for castle silhouette
        await rcon.SendCommandAsync(
            $"fill {x + 4} {y + 21} {z} {x + 5} {y + 21} {z} minecraft:stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 9} {y + 21} {z} {x + 10} {y + 21} {z} minecraft:stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 4} {y + 21} {z + s} {x + 5} {y + 21} {z + s} minecraft:stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 9} {y + 21} {z + s} {x + 10} {y + 21} {z + s} minecraft:stone_bricks", ct);

        // === CORNER TURRETS: rise above parapet with stair caps and pinnacles ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 21} {z} {x + 2} {y + 21} {z + 2} minecraft:stone_brick_stairs[facing=south,half=top]", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s - 2} {y + 21} {z} {x + s} {y + 21} {z + 2} minecraft:stone_brick_stairs[facing=south,half=top]", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 21} {z + s - 2} {x + 2} {y + 21} {z + s} minecraft:stone_brick_stairs[facing=north,half=top]", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s - 2} {y + 21} {z + s - 2} {x + s} {y + 21} {z + s} minecraft:stone_brick_stairs[facing=north,half=top]", ct);
        // Pinnacle wall posts atop turrets
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 22} {z + 1} minecraft:stone_brick_wall", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 22} {z + 1} minecraft:stone_brick_wall", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 22} {z + s - 1} minecraft:stone_brick_wall", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 22} {z + s - 1} minecraft:stone_brick_wall", ct);

        // === 4 STANDING BANNERS on turret pinnacles ===
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 23} {z + 1} {banner}[rotation=0]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 23} {z + 1} {banner}[rotation=0]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 23} {z + s - 1} {banner}[rotation=8]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 23} {z + s - 1} {banner}[rotation=8]", ct);

        // === GATEHOUSE: clean entrance with decorative arch ===
        await rcon.SendCommandAsync(
            $"fill {x + half - 2} {y + 1} {z} {x + half + 2} {y + 5} {z} minecraft:stone_bricks", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + half - 1} {y + 5} {z} minecraft:stone_brick_stairs[facing=east,half=top]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + half + 1} {y + 5} {z} minecraft:stone_brick_stairs[facing=west,half=top]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + half} {y + 5} {z} minecraft:chiseled_stone_bricks", ct);
        // Clear doorway opening: 3 wide, 4 tall (y+1 to y+4) — clean entrance
        await rcon.SendCommandAsync(
            $"fill {x + half - 1} {y + 1} {z} {x + half + 1} {y + 4} {z} minecraft:air", ct);

        // === INTERIOR: FLOOR PLATFORMS ===
        // Second floor platform at y+7 (oak planks)
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 7} {z + 1} {x + s - 1} {y + 7} {z + s - 1} minecraft:oak_planks", ct);
        // Third floor platform at y+13 (oak planks)
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 13} {z + 1} {x + s - 1} {y + 13} {z + s - 1} minecraft:oak_planks", ct);

        // === SPIRAL STAIRCASE: oak stairs along inner walls ===
        // Flight 1 (ground to second floor): along north wall (z+1 side), going east
        await rcon.SendCommandAsync(
            $"setblock {x + 2} {y + 1} {z + 1} minecraft:oak_stairs[facing=east]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 3} {y + 2} {z + 1} minecraft:oak_stairs[facing=east]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 4} {y + 3} {z + 1} minecraft:oak_stairs[facing=east]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 5} {y + 4} {z + 1} minecraft:oak_stairs[facing=east]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 6} {y + 5} {z + 1} minecraft:oak_stairs[facing=east]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 7} {y + 6} {z + 1} minecraft:oak_stairs[facing=east]", ct);
        // Landing at y+7 — already covered by second floor platform
        // Clear stairwell hole in second floor for access
        await rcon.SendCommandAsync(
            $"fill {x + 2} {y + 7} {z + 1} {x + 7} {y + 7} {z + 2} minecraft:air", ct);

        // Flight 2 (second to third floor): along east wall (x+s-1 side), going south
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 8} {z + 2} minecraft:oak_stairs[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 9} {z + 3} minecraft:oak_stairs[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 10} {z + 4} minecraft:oak_stairs[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 11} {z + 5} minecraft:oak_stairs[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 12} {z + 6} minecraft:oak_stairs[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 13} {z + 7} minecraft:oak_stairs[facing=south]", ct);
        // Clear stairwell hole in third floor for access
        await rcon.SendCommandAsync(
            $"fill {x + s - 2} {y + 13} {z + 2} {x + s - 1} {y + 13} {z + 7} minecraft:air", ct);

        // === GROUND FLOOR FURNITURE (y+1 to y+6) ===
        // Crafting table
        await rcon.SendCommandAsync(
            $"setblock {x + 2} {y + 1} {z + s - 1} minecraft:crafting_table", ct);
        // Resource name sign on back wall
        await rcon.SendCommandAsync(
            $"setblock {x + half} {y + 3} {z + s} minecraft:oak_wall_sign[facing=north]", ct);
        var signCmd = "data merge block " + $"{x + half} {y + 3} {z + s}" +
            " {front_text:{messages:[\"\"," +
            "\"" + info.Name + "\"," +
            "\"(Project)\"," +
            "\"\"]}}";
        await rcon.SendCommandAsync(signCmd, ct);
        // 4 torches on walls
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 3} {z + 4} minecraft:wall_torch[facing=east]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 3} {z + 4} minecraft:wall_torch[facing=west]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 3} {y + 3} {z + 1} minecraft:wall_torch[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 4} {y + 3} {z + s} minecraft:wall_torch[facing=north]", ct);

        // === SECOND FLOOR FURNITURE (y+8 to y+12) ===
        // Enchanting table centered
        await rcon.SendCommandAsync(
            $"setblock {x + half} {y + 8} {z + half} minecraft:enchanting_table", ct);
        // Bookshelves lining walls
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 8} {z + s - 1} {x + 5} {y + 9} {z + s - 1} minecraft:bookshelf", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 8} {z + 1} {x + 1} {y + 9} {z + 5} minecraft:bookshelf", ct);

        // === THIRD FLOOR FURNITURE (y+14 to y+18) — observation deck ===
        // Lectern
        await rcon.SendCommandAsync(
            $"setblock {x + half} {y + 14} {z + half} minecraft:lectern[facing=south]", ct);

        return new DoorPosition(x + half, y + 4, z);
    }

    /// <summary>
    /// Warehouse — wide, flat, cargo bay feel. Container (Docker) resources.
    /// Standard: Iron block frame, purple stained glass windows, 7×7 footprint, 5 blocks tall.
    /// Grand: Iron block frame with deepslate brick infill, 15×15 footprint, 8 blocks tall,
    /// 5-wide cargo bay entrance, loading dock, furnished interior.
    /// </summary>
    private async Task<DoorPosition> BuildWarehouseAsync(int x, int y, int z, ResourceInfo info, CancellationToken ct)
    {
        if (VillageLayout.StructureSize >= 15)
        {
            return await BuildGrandWarehouseAsync(x, y, z, info, ct);
        }

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

        return new DoorPosition(x + 3, y + 3, z);
    }

    /// <summary>
    /// Grand Warehouse — enlarged 15×15 container building with loading dock and furnished interior.
    /// Iron block frame with deepslate brick infill panels, 5-wide cargo bay entrance,
    /// purple stained glass clerestory windows, stone brick loading dock with fence railings,
    /// 8 barrels, 2 chest rows, iron block columns, hanging lanterns, resource name sign.
    /// </summary>
    private async Task<DoorPosition> BuildGrandWarehouseAsync(int x, int y, int z, ResourceInfo info, CancellationToken ct)
    {
        var s = VillageLayout.StructureSize - 1; // 14

        // === FOUNDATION: 15×15 iron block floor ===
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z} {x + s} {y} {z + s} minecraft:iron_block", ct);

        // === WALLS: deepslate brick infill, 7 blocks tall (y+1 to y+7) ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z} {x + s} {y + 7} {z + s} minecraft:deepslate_bricks hollow", ct);

        // === IRON BLOCK FRAME: corner pillars (full height) ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z} {x} {y + 7} {z} minecraft:iron_block", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 1} {z} {x + s} {y + 7} {z} minecraft:iron_block", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z + s} {x} {y + 7} {z + s} minecraft:iron_block", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 1} {z + s} {x + s} {y + 7} {z + s} minecraft:iron_block", ct);

        // === FLAT ROOF: iron block ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 8} {z} {x + s} {y + 8} {z + s} minecraft:iron_block", ct);

        // === CARGO BAY ENTRANCE: 5-wide × 4-tall on front face (z-min) ===
        await rcon.SendCommandAsync(
            $"fill {x + 5} {y + 1} {z} {x + 9} {y + 4} {z} minecraft:air", ct);

        // === CLERESTORY WINDOWS: purple stained glass strip near roofline (y+7) ===
        // Front wall (skip cargo bay area)
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 7} {z} {x + 4} {y + 7} {z} minecraft:purple_stained_glass", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 10} {y + 7} {z} {x + 13} {y + 7} {z} minecraft:purple_stained_glass", ct);
        // Back wall
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 7} {z + s} {x + 13} {y + 7} {z + s} minecraft:purple_stained_glass", ct);
        // Side walls
        await rcon.SendCommandAsync(
            $"fill {x} {y + 7} {z + 1} {x} {y + 7} {z + 13} minecraft:purple_stained_glass", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 7} {z + 1} {x + s} {y + 7} {z + 13} minecraft:purple_stained_glass", ct);

        // === LOADING DOCK: stone brick platform extending 2 blocks from entrance ===
        await rcon.SendCommandAsync(
            $"fill {x + 4} {y} {z - 1} {x + 10} {y} {z - 2} minecraft:stone_bricks", ct);
        // Fence railings on dock sides
        await rcon.SendCommandAsync(
            $"fill {x + 4} {y + 1} {z - 1} {x + 4} {y + 1} {z - 2} minecraft:oak_fence", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 10} {y + 1} {z - 1} {x + 10} {y + 1} {z - 2} minecraft:oak_fence", ct);

        // === INTERIOR: iron block columns at quarter-points ===
        await rcon.SendCommandAsync(
            $"fill {x + 4} {y + 1} {z + 4} {x + 4} {y + 6} {z + 4} minecraft:iron_block", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 10} {y + 1} {z + 4} {x + 10} {y + 6} {z + 4} minecraft:iron_block", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 4} {y + 1} {z + 10} {x + 4} {y + 6} {z + 10} minecraft:iron_block", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 10} {y + 1} {z + 10} {x + 10} {y + 6} {z + 10} minecraft:iron_block", ct);

        // === INTERIOR: 8 barrels (4×2 grid) ===
        await rcon.SendCommandAsync(
            $"setblock {x + 2} {y + 1} {z + 12} minecraft:barrel[facing=up]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 3} {y + 1} {z + 12} minecraft:barrel[facing=up]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 4} {y + 1} {z + 12} minecraft:barrel[facing=up]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 5} {y + 1} {z + 12} minecraft:barrel[facing=up]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 2} {y + 1} {z + 11} minecraft:barrel[facing=up]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 3} {y + 1} {z + 11} minecraft:barrel[facing=up]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 4} {y + 1} {z + 11} minecraft:barrel[facing=up]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 5} {y + 1} {z + 11} minecraft:barrel[facing=up]", ct);

        // === INTERIOR: 2 chest rows ===
        await rcon.SendCommandAsync(
            $"setblock {x + 9} {y + 1} {z + 12} minecraft:chest[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 10} {y + 1} {z + 12} minecraft:chest[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 11} {y + 1} {z + 12} minecraft:chest[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 12} {y + 1} {z + 12} minecraft:chest[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 9} {y + 1} {z + 11} minecraft:chest[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 10} {y + 1} {z + 11} minecraft:chest[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 11} {y + 1} {z + 11} minecraft:chest[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 12} {y + 1} {z + 11} minecraft:chest[facing=south]", ct);

        // === INTERIOR: hanging lanterns from ceiling ===
        await rcon.SendCommandAsync(
            $"setblock {x + 4} {y + 7} {z + 7} minecraft:lantern[hanging=true]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 7} {y + 7} {z + 7} minecraft:lantern[hanging=true]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 10} {y + 7} {z + 7} minecraft:lantern[hanging=true]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 7} {y + 7} {z + 3} minecraft:lantern[hanging=true]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 7} {y + 7} {z + 11} minecraft:lantern[hanging=true]", ct);

        // === INTERIOR: resource name sign on back wall ===
        await rcon.SendCommandAsync(
            $"setblock {x + 7} {y + 3} {z + s} minecraft:oak_wall_sign[facing=north]", ct);
        var signCmd = "data merge block " + $"{x + 7} {y + 3} {z + s}" +
            " {front_text:{messages:[\"\"," +
            "\"" + info.Name + "\"," +
            "\"(Container)\"," +
            "\"\"]}}";
        await rcon.SendCommandAsync(signCmd, ct);

        return new DoorPosition(x + 7, y + 4, z);
    }

    /// <summary>
    /// Workshop — building with chimney and workbench vibe. Executable resources.
    /// Standard (7×7): Oak planks walls, cyan stained glass accents, 6 blocks tall.
    /// Grand (15×15): Spruce log frame, A-frame peaked roof, loft, full tool stations, 10 blocks tall.
    /// </summary>
    private async Task<DoorPosition> BuildWorkshopAsync(int x, int y, int z, CancellationToken ct)
    {
        if (VillageLayout.StructureSize >= 15)
        {
            return await BuildGrandWorkshopAsync(x, y, z, ct);
        }

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

        return new DoorPosition(x + 3, y + 2, z);
    }

    /// <summary>
    /// Grand Workshop — enlarged 15×15 executable building with loft and tool stations.
    /// Oak plank walls with spruce log frame (corner posts + horizontal beams at y+5).
    /// A-frame peaked roof with spruce stair shingles. 2×2 cobblestone chimney with campfire.
    /// Cyan stained glass windows with flower boxes under front windows.
    /// Interior: crafting table, smithing table, stonecutter, anvil, grindstone, furnace, brewing stand.
    /// Loft at y+6: half-floor accessible by ladder, storage barrels, bookshelf.
    /// ~60 RCON commands.
    /// </summary>
    private async Task<DoorPosition> BuildGrandWorkshopAsync(int x, int y, int z, CancellationToken ct)
    {
        var s = VillageLayout.StructureSize - 1; // 14

        // === FOUNDATION: 15×15 oak planks floor ===
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z} {x + s} {y} {z + s} minecraft:oak_planks", ct);

        // === WALLS: hollow box, 5 blocks tall (y+1 to y+5) ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z} {x + s} {y + 5} {z + s} minecraft:oak_planks hollow", ct);

        // === SPRUCE LOG FRAME: corner posts (y+1 to y+5) ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z} {x} {y + 5} {z} minecraft:spruce_log", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 1} {z} {x + s} {y + 5} {z} minecraft:spruce_log", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 1} {z + s} {x} {y + 5} {z + s} minecraft:spruce_log", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 1} {z + s} {x + s} {y + 5} {z + s} minecraft:spruce_log", ct);

        // === SPRUCE LOG FRAME: horizontal beams at y+5 (top of walls) ===
        await rcon.SendCommandAsync(
            $"fill {x} {y + 5} {z} {x + s} {y + 5} {z} minecraft:spruce_log", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 5} {z + s} {x + s} {y + 5} {z + s} minecraft:spruce_log", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 5} {z + 1} {x} {y + 5} {z + s - 1} minecraft:spruce_log", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 5} {z + 1} {x + s} {y + 5} {z + s - 1} minecraft:spruce_log", ct);

        // === A-FRAME PEAKED ROOF: spruce stair shingles (along Z axis) ===
        // Layer 1 (y+6): outer eaves
        await rcon.SendCommandAsync(
            $"fill {x} {y + 6} {z} {x + s} {y + 6} {z + 3} minecraft:spruce_stairs[facing=south,half=bottom]", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 6} {z + s - 3} {x + s} {y + 6} {z + s} minecraft:spruce_stairs[facing=north,half=bottom]", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 6} {z + 4} {x + s} {y + 6} {z + s - 4} minecraft:oak_planks", ct);
        // Layer 2 (y+7): mid roof
        await rcon.SendCommandAsync(
            $"fill {x} {y + 7} {z + 2} {x + s} {y + 7} {z + 5} minecraft:spruce_stairs[facing=south,half=bottom]", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 7} {z + s - 5} {x + s} {y + 7} {z + s - 2} minecraft:spruce_stairs[facing=north,half=bottom]", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 7} {z + 6} {x + s} {y + 7} {z + s - 6} minecraft:oak_planks", ct);
        // Layer 3 (y+8): upper roof
        await rcon.SendCommandAsync(
            $"fill {x} {y + 8} {z + 5} {x + s} {y + 8} {z + 6} minecraft:spruce_stairs[facing=south,half=bottom]", ct);
        await rcon.SendCommandAsync(
            $"fill {x} {y + 8} {z + s - 6} {x + s} {y + 8} {z + s - 5} minecraft:spruce_stairs[facing=north,half=bottom]", ct);
        // Layer 4 (y+9): ridge cap
        await rcon.SendCommandAsync(
            $"fill {x} {y + 9} {z + 7} {x + s} {y + 9} {z + 7} minecraft:spruce_slab", ct);

        // === CHIMNEY: 2×2 cobblestone at back-right corner ===
        await rcon.SendCommandAsync(
            $"fill {x + s - 1} {y + 6} {z + s - 1} {x + s} {y + 10} {z + s} minecraft:cobblestone", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s - 1} {y + 10} {z + s - 1} {x + s} {y + 10} {z + s} minecraft:campfire", ct);

        // === CYAN STAINED GLASS WINDOWS ===
        // Front wall (z): 2 windows flanking the door
        await rcon.SendCommandAsync(
            $"fill {x + 3} {y + 3} {z} {x + 4} {y + 4} {z} minecraft:cyan_stained_glass", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s - 4} {y + 3} {z} {x + s - 3} {y + 4} {z} minecraft:cyan_stained_glass", ct);
        // Side walls
        await rcon.SendCommandAsync(
            $"fill {x} {y + 3} {z + 5} {x} {y + 4} {z + 6} minecraft:cyan_stained_glass", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s} {y + 3} {z + 5} {x + s} {y + 4} {z + 6} minecraft:cyan_stained_glass", ct);
        // Back wall
        await rcon.SendCommandAsync(
            $"fill {x + 5} {y + 3} {z + s} {x + 6} {y + 4} {z + s} minecraft:cyan_stained_glass", ct);
        await rcon.SendCommandAsync(
            $"fill {x + s - 6} {y + 3} {z + s} {x + s - 5} {y + 4} {z + s} minecraft:cyan_stained_glass", ct);

        // === FLOWER BOXES under front windows ===
        await rcon.SendCommandAsync(
            $"setblock {x + 3} {y + 2} {z} minecraft:flower_pot", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 3} {y + 2} {z} minecraft:flower_pot", ct);

        // === DOOR: 3 blocks wide, 3 tall, centered on front wall ===
        await rcon.SendCommandAsync(
            $"fill {x + 6} {y + 1} {z} {x + 8} {y + 3} {z} minecraft:air", ct);

        // === INTERIOR: tool stations along back wall ===
        await rcon.SendCommandAsync(
            $"setblock {x + 2} {y + 1} {z + s - 1} minecraft:crafting_table", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 4} {y + 1} {z + s - 1} minecraft:smithing_table", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 6} {y + 1} {z + s - 1} minecraft:stonecutter", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 8} {y + 1} {z + s - 1} minecraft:anvil", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 10} {y + 1} {z + s - 1} minecraft:grindstone", ct);
        // Furnace against back wall
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 1} {z + s - 1} minecraft:furnace[facing=south]", ct);
        // Brewing stand in opposite back corner
        await rcon.SendCommandAsync(
            $"setblock {x + s - 1} {y + 1} {z + s - 1} minecraft:brewing_stand", ct);

        // === LOFT at y+6: half-floor (back half) ===
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 6} {z + 7} {x + s - 1} {y + 6} {z + s - 1} minecraft:oak_planks", ct);

        // Loft railing (fence along the loft edge)
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 7} {z + 7} {x + s - 1} {y + 7} {z + 7} minecraft:oak_fence", ct);

        // Ladder access to loft (inside, against side wall)
        await rcon.SendCommandAsync(
            $"fill {x + 1} {y + 1} {z + 7} {x + 1} {y + 6} {z + 7} minecraft:ladder[facing=east]", ct);

        // Loft furnishing: storage barrels and bookshelf
        await rcon.SendCommandAsync(
            $"setblock {x + 3} {y + 7} {z + s - 1} minecraft:barrel[facing=up]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 5} {y + 7} {z + s - 1} minecraft:barrel[facing=up]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 7} {y + 7} {z + s - 1} minecraft:barrel[facing=up]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 2} {y + 7} {z + s - 1} minecraft:bookshelf", ct);

        // === INTERIOR LIGHTING: lanterns ===
        await rcon.SendCommandAsync(
            $"setblock {x + 4} {y + 5} {z + 4} minecraft:lantern[hanging=true]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + s - 4} {y + 5} {z + 4} minecraft:lantern[hanging=true]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 7} {y + 5} {z + s - 4} minecraft:lantern[hanging=true]", ct);

        return new DoorPosition(x + 7, y + 3, z);
    }

    /// <summary>
    /// Cottage — humble default dwelling. Unknown/Other resource types.
    /// Standard (7×7): Cobblestone walls, language-colored wool trim, 5 blocks tall.
    /// Grand (15×15): 8 blocks tall, cobblestone lower / oak plank upper walls,
    /// language-colored wool trim at roof level, cobblestone slab pitched roof,
    /// flower pots and window boxes on front face, furnished interior.
    /// </summary>
    private async Task<DoorPosition> BuildCottageAsync(int x, int y, int z, ResourceInfo info, CancellationToken ct)
    {
        if (VillageLayout.StructureSize == 15)
        {
            return await BuildGrandCottageAsync(x, y, z, info, ct);
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

        return new DoorPosition(x + 3, y + 2, z);
    }

    /// <summary>
    /// Grand Cottage — 15×15 footprint, 8 blocks tall.
    /// Cobblestone lower walls (y+1 to y+4), oak plank upper walls (y+5 to y+7).
    /// Language-colored wool trim band at roof level (y+7).
    /// Cobblestone slab pitched roof. Flower pots and window boxes on front face.
    /// Interior: bed, crafting table, bookshelf, furnace, 2 chests, potted flowers, 4 torches.
    /// ~40-50 RCON commands.
    /// </summary>
    private async Task<DoorPosition> BuildGrandCottageAsync(int x, int y, int z, ResourceInfo info, CancellationToken ct)
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

        return new DoorPosition(x + half, y + 2, z);
    }

    /// <summary>
    /// Cylinder — round building evoking the database cylinder icon. Database resources.
    /// Standard (7×7): Smooth stone walls, polished deepslate floor and dome, radius 3.
    /// Grand (15×15): Two-floor silo with copper pillar, iron server racks, bookshelf ring.
    /// </summary>
    private async Task<DoorPosition> BuildCylinderAsync(int x, int y, int z, CancellationToken ct)
    {
        if (VillageLayout.StructureSize >= 15)
        {
            return await BuildGrandCylinderAsync(x, y, z, ct);
        }

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

        return new DoorPosition(x + 3, y + 2, z);
    }

    /// <summary>
    /// Grand Silo — enlarged database cylinder with two interior floors and central data pillar.
    /// 15×15 footprint (radius 7), 12 blocks tall. Polished deepslate + smooth stone walls,
    /// copper accent bands at mid-height and top, domed deepslate slab roof.
    /// Lower floor: iron block server rack ring, copper center island, 4 redstone lamps.
    /// Upper floor: bookshelf ring, enchanting table, ladder access.
    /// Central copper pillar from floor to ceiling.
    /// ~120-140 RCON commands — uses pre-calculated circle coordinates and /fill per row.
    /// </summary>
    private async Task<DoorPosition> BuildGrandCylinderAsync(int x, int y, int z, CancellationToken ct)
    {
        // Pre-calculated radius-7 circle row spans: (dz, xMin, xMax) relative to origin
        // Symmetric circle inscribed in the 15×15 footprint (indices 0–14)
        (int dz, int x1, int x2)[] circleRows =
        [
            (0,  4, 10),
            (1,  3, 11),
            (2,  2, 12),
            (3,  1, 13),
            (4,  1, 13),
            (5,  0, 14),
            (6,  0, 14),
            (7,  0, 14),
            (8,  0, 14),
            (9,  0, 14),
            (10, 1, 13),
            (11, 1, 13),
            (12, 2, 12),
            (13, 3, 11),
            (14, 4, 10),
        ];

        // === FLOOR (y+0): polished deepslate disc ===
        foreach (var (dz, x1, x2) in circleRows)
        {
            await rcon.SendCommandAsync(
                $"fill {x + x1} {y} {z + dz} {x + x2} {y} {z + dz} minecraft:polished_deepslate", ct);
        }

        // === WALLS (y+1 to y+10): perimeter shell ===
        // Smooth stone (y+1 to y+4), copper accent band (y+5 to y+6),
        // polished deepslate (y+7 to y+9), copper accent band (y+10)
        for (int layer = 1; layer <= 10; layer++)
        {
            string wallBlock = layer is 5 or 6 or 10
                ? "minecraft:cut_copper"
                : (layer <= 4 ? "minecraft:smooth_stone" : "minecraft:polished_deepslate");

            foreach (var (dz, x1, x2) in circleRows)
            {
                await rcon.SendCommandAsync(
                    $"fill {x + x1} {y + layer} {z + dz} {x + x2} {y + layer} {z + dz} {wallBlock}", ct);
            }
        }

        // Interior circle rows (hollowed out inside the walls)
        (int dz, int x1, int x2)[] interiorRows =
        [
            (2,  4, 10),
            (3,  3, 11),
            (4,  2, 12),
            (5,  2, 12),
            (6,  1, 13),
            (7,  1, 13),
            (8,  1, 13),
            (9,  2, 12),
            (10, 2, 12),
            (11, 3, 11),
            (12, 4, 10),
        ];

        // === INTERIOR AIR (y+1 to y+10): hollow the cylinder ===
        for (int layer = 1; layer <= 10; layer++)
        {
            foreach (var (dz, x1, x2) in interiorRows)
            {
                await rcon.SendCommandAsync(
                    $"fill {x + x1} {y + layer} {z + dz} {x + x2} {y + layer} {z + dz} minecraft:air", ct);
            }
        }

        // (Doorway cleared after interior furnishing — see IRON DOOR ENTRANCE below)

        // === UPPER FLOOR (y+6): polished deepslate disc at mid-height ===
        foreach (var (dz, x1, x2) in interiorRows)
        {
            await rcon.SendCommandAsync(
                $"fill {x + x1} {y + 6} {z + dz} {x + x2} {y + 6} {z + dz} minecraft:polished_deepslate", ct);
        }

        // === UPPER FLOOR AIR (y+7 to y+10): re-clear above the floor ===
        for (int layer = 7; layer <= 10; layer++)
        {
            foreach (var (dz, x1, x2) in interiorRows)
            {
                await rcon.SendCommandAsync(
                    $"fill {x + x1} {y + layer} {z + dz} {x + x2} {y + layer} {z + dz} minecraft:air", ct);
            }
        }

        // === DOME ROOF (y+11): deepslate tile slab outer ring ===
        foreach (var (dz, x1, x2) in circleRows)
        {
            await rcon.SendCommandAsync(
                $"fill {x + x1} {y + 11} {z + dz} {x + x2} {y + 11} {z + dz} minecraft:deepslate_tile_slab", ct);
        }

        // === DOME CAP (y+12): smaller polished deepslate slab cap ===
        (int dz, int x1, int x2)[] domeCapRows =
        [
            (3,  4, 10),
            (4,  3, 11),
            (5,  3, 11),
            (6,  2, 12),
            (7,  2, 12),
            (8,  2, 12),
            (9,  3, 11),
            (10, 3, 11),
            (11, 4, 10),
        ];

        foreach (var (dz, x1, x2) in domeCapRows)
        {
            await rcon.SendCommandAsync(
                $"fill {x + x1} {y + 12} {z + dz} {x + x2} {y + 12} {z + dz} minecraft:polished_deepslate_slab", ct);
        }

        // === DOME PEAK (y+13): small deepslate cap ===
        await rcon.SendCommandAsync(
            $"fill {x + 5} {y + 13} {z + 5} {x + 9} {y + 13} {z + 9} minecraft:polished_deepslate_slab", ct);

        // === CENTRAL COPPER PILLAR: floor to ceiling at center (x+7, z+7) ===
        await rcon.SendCommandAsync(
            $"fill {x + 7} {y} {z + 7} {x + 7} {y + 12} {z + 7} minecraft:copper_block", ct);

        // === LOWER FLOOR (y+1 to y+5): Server rack ring + copper center island ===
        // Iron block server rack ring along interior perimeter (y+1)
        await rcon.SendCommandAsync(
            $"fill {x + 4} {y + 1} {z + 3} {x + 4} {y + 1} {z + 11} minecraft:iron_block", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 10} {y + 1} {z + 3} {x + 10} {y + 1} {z + 11} minecraft:iron_block", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 5} {y + 1} {z + 2} {x + 9} {y + 1} {z + 2} minecraft:iron_block", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 5} {y + 1} {z + 12} {x + 9} {y + 1} {z + 12} minecraft:iron_block", ct);

        // Copper block center island (3×3 around central pillar)
        await rcon.SendCommandAsync(
            $"fill {x + 6} {y} {z + 6} {x + 8} {y} {z + 8} minecraft:copper_block", ct);

        // 4 Redstone lamps at cardinal positions (avoiding the entrance path at z+2..z+3)
        await rcon.SendCommandAsync($"setblock {x + 7} {y + 1} {z + 10} minecraft:redstone_lamp[lit=true]", ct);
        await rcon.SendCommandAsync($"setblock {x + 3} {y + 1} {z + 7} minecraft:redstone_lamp[lit=true]", ct);
        await rcon.SendCommandAsync($"setblock {x + 11} {y + 1} {z + 7} minecraft:redstone_lamp[lit=true]", ct);
        await rcon.SendCommandAsync($"setblock {x + 7} {y + 1} {z + 4} minecraft:redstone_lamp[lit=true]", ct);

        // === IRON DOOR ENTRANCE (placed last so nothing overwrites it) ===
        // Clear only a 1-wide passage through the circular wall (z+0 to z+1), 3 tall
        await rcon.SendCommandAsync(
            $"fill {x + 7} {y + 1} {z} {x + 7} {y + 3} {z + 1} minecraft:air", ct);
        // Place door at z+0 (outer wall face)
        await rcon.SendCommandAsync(
            $"setblock {x + 7} {y + 1} {z} minecraft:iron_door[facing=south,half=lower,hinge=left]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 7} {y + 2} {z} minecraft:iron_door[facing=south,half=upper,hinge=left]", ct);

        // === LADDER ACCESS to upper floor: along the central pillar ===
        for (int ly = 1; ly <= 6; ly++)
        {
            await rcon.SendCommandAsync(
                $"setblock {x + 8} {y + ly} {z + 7} minecraft:ladder[facing=west]", ct);
        }

        // === UPPER FLOOR (y+7 to y+10): Bookshelf ring + enchanting table ===
        await rcon.SendCommandAsync(
            $"fill {x + 4} {y + 7} {z + 3} {x + 4} {y + 7} {z + 11} minecraft:bookshelf", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 10} {y + 7} {z + 3} {x + 10} {y + 7} {z + 11} minecraft:bookshelf", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 5} {y + 7} {z + 2} {x + 9} {y + 7} {z + 2} minecraft:bookshelf", ct);
        await rcon.SendCommandAsync(
            $"fill {x + 5} {y + 7} {z + 12} {x + 9} {y + 7} {z + 12} minecraft:bookshelf", ct);

        // Enchanting table near center
        await rcon.SendCommandAsync(
            $"setblock {x + 6} {y + 7} {z + 7} minecraft:enchanting_table", ct);

        // Signs with connection info placeholders on upper floor walls
        await rcon.SendCommandAsync(
            $"setblock {x + 5} {y + 8} {z + 3} minecraft:oak_wall_sign[facing=south]", ct);
        await rcon.SendCommandAsync(
            $"setblock {x + 9} {y + 8} {z + 3} minecraft:oak_wall_sign[facing=south]", ct);

        logger.LogInformation("Grand Silo (database cylinder) built at ({X},{Y},{Z})", x, y, z);

        return new DoorPosition(x + 7, y + 2, z);
    }

    /// <summary>
    /// AzureThemed — modified Cottage with Azure color palette. Non-database Azure resources.
    /// Standard (7×7): Light blue concrete walls, blue concrete trim, stained glass roof.
    /// Grand (15×15): 8 blocks tall, pilaster strips, 3×3 skylight, banners on all 4 roof corners,
    /// light blue carpet floor, blue stained glass internal windows, brewing stand and cauldron.
    /// </summary>
    private async Task<DoorPosition> BuildAzureThemedAsync(int x, int y, int z, ResourceInfo info, CancellationToken ct)
    {
        if (VillageLayout.StructureSize == 15)
        {
            return await BuildGrandAzurePavilionAsync(x, y, z, info, ct);
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

        return new DoorPosition(x + 3, y + 2, z);
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
    private async Task<DoorPosition> BuildGrandAzurePavilionAsync(int x, int y, int z, ResourceInfo info, CancellationToken ct)
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

        return new DoorPosition(x + half, y + 2, z);
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
            "Warehouse" when isGrand => y + 9,
            "Warehouse" => y + 6,
            "Workshop" when isGrand => y + 10,
            "Workshop" => y + 7,
            "Cylinder" => isGrand ? y + 13 : y + 7,
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
    /// Places a health indicator lamp directly above the door.
    /// Position is derived from the door — no per-building-type logic needed.
    /// Healthy = glowstone (always lit), Unhealthy = redstone lamp (unlit), Unknown = sea lantern.
    /// </summary>
    private async Task PlaceHealthIndicatorAsync(DoorPosition door, ResourceStatus status, CancellationToken ct)
    {
        var lampBlock = status switch
        {
            ResourceStatus.Healthy => "minecraft:glowstone",
            ResourceStatus.Unhealthy => "minecraft:redstone_lamp",
            _ => "minecraft:sea_lantern"
        };

        var (lampX, lampY, lampZ) = door.GlowBlock;
        await rcon.SendCommandAsync(
            $"setblock {lampX} {lampY} {lampZ} {lampBlock}", ct);
    }

    /// <summary>
    /// Places a sign just outside the door with the resource name and status.
    /// Position is derived from the door — one block in front, offset to the right.
    /// </summary>
    private async Task PlaceSignAsync(DoorPosition door, ResourceInfo info, CancellationToken ct)
    {
        var (signX, signY, signZ) = door.Sign;

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
