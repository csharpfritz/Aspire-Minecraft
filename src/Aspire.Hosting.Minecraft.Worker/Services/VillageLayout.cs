namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Resource categories for neighborhood grouping.
/// Each category is placed in a different zone quadrant when neighborhoods are enabled.
/// </summary>
internal enum ResourceCategory
{
    /// <summary>.NET project resources → Watchtower buildings (NW zone).</summary>
    DotNetProject,
    /// <summary>Azure-hosted resources (non-database) → AzureThemed buildings (NE zone).</summary>
    Azure,
    /// <summary>Containers and databases → Warehouse/Cylinder buildings (SW zone).</summary>
    ContainerOrDatabase,
    /// <summary>Executable resources (Python, Node.js) → Workshop buildings (SE zone).</summary>
    Executable,
    /// <summary>Unknown or unclassified resources → Cottage buildings (SE zone, after executables).</summary>
    Other
}

/// <summary>
/// Describes a group of resources assigned to a specific zone in the village.
/// </summary>
/// <param name="Category">The resource category for this neighborhood.</param>
/// <param name="OriginX">X coordinate of the zone's southwest corner.</param>
/// <param name="OriginZ">Z coordinate of the zone's southwest corner.</param>
/// <param name="Resources">Dependency-ordered resource names within this zone.</param>
internal record Neighborhood(
    ResourceCategory Category,
    int OriginX,
    int OriginZ,
    List<string> Resources);

/// <summary>
/// Complete neighborhood assignment plan mapping every resource to a zone position.
/// </summary>
/// <param name="Neighborhoods">All neighborhood zones with their resource lists.</param>
/// <param name="ResourcePositions">Maps each resource name to its (x, z) origin position.</param>
internal record NeighborhoodPlan(
    List<Neighborhood> Neighborhoods,
    Dictionary<string, (int x, int z)> ResourcePositions);

/// <summary>
/// Centralizes the 2D grid layout coordinates for the resource village.
/// All services that place things per-resource should use this to get consistent positions.
/// 
/// <para><b>Minecraft Superflat Coordinate System (Y-axis):</b></para>
/// <list type="bullet">
/// <item>Y=-64 to -62: Bedrock layer (unbreakable foundation)</item>
/// <item>Y=-61: Dirt block</item>
/// <item>Y=-60 (BaseY): Grass block surface — where structures place their floors</item>
/// <item>Y=-59: Air (where players walk)</item>
/// </list>
/// 
/// <para><b>Village Grid Layout (X/Z plane):</b></para>
/// <list type="bullet">
/// <item>Origin: (BaseX=10, BaseZ=0) — southwest corner of first structure</item>
/// <item>2-column grid: buildings arranged in 2 columns (X) × N rows (Z) on a single Y-plane</item>
/// <item>Spacing=36 blocks center-to-center</item>
/// <item>Each structure footprint: 15×15 blocks</item>
/// <item>Index 0 → col 0 row 0, Index 1 → col 1 row 0, Index 2 → col 0 row 1, etc.</item>
/// </list>
/// 
/// <para><b>Z-Coordinate Conventions:</b></para>
/// <list type="bullet">
/// <item>Front wall (entrance): Always on Z-min side (south-facing)</item>
/// <item>Hollow structures (watchtower): Inner 5×5 box starts at origin+(1,1,1), so front wall is at z+1</item>
/// <item>Solid structures (warehouse/workshop/cottage): Front wall is at z (origin edge)</item>
/// <item>Doors must clear at the actual wall Z coordinate, not origin Z</item>
/// </list>
/// 
/// <para><b>Neighborhood Zones (when enabled via ASPIRE_FEATURE_NEIGHBORHOODS):</b></para>
/// <list type="bullet">
/// <item>2×2 quadrant layout with ZoneGap between quadrants</item>
/// <item>NW quadrant: .NET projects</item>
/// <item>NE quadrant: Azure services</item>
/// <item>SW quadrant: Containers and databases</item>
/// <item>SE quadrant: Executables (Python, Node.js)</item>
/// <item>Each zone uses a 2-column sub-grid for its buildings</item>
/// </list>
/// </summary>
internal static class VillageLayout
{
    /// <summary>Base X coordinate for the village grid (southwest corner of first structure).</summary>
    public const int BaseX = 10;

    /// <summary>
    /// Default surface level Y for superflat world (grass block layer at Y=-60).
    /// Used as a fallback when terrain detection fails or hasn't run yet.
    /// </summary>
    public const int BaseY = -60;

    /// <summary>
    /// Detected surface Y coordinate. Set by <see cref="TerrainProbeService"/> at startup.
    /// Defaults to <see cref="BaseY"/> for backward compatibility with superflat worlds.
    /// All services should use this instead of <see cref="BaseY"/> for Y positioning.
    /// </summary>
    public static int SurfaceY { get; set; } = BaseY;

    /// <summary>Base Z coordinate for the village grid.</summary>
    public const int BaseZ = 0;

    /// <summary>Spacing between structure origins (center-to-center) in blocks.</summary>
    public static int Spacing { get; private set; } = 36;

    /// <summary>Number of columns in the village grid.</summary>
    public const int Columns = 2;

    /// <summary>Structure footprint width/depth (15×15 blocks).</summary>
    public static int StructureSize { get; private set; } = 15;

    /// <summary>
    /// Clearance (in blocks) between village bounds and fence perimeter.
    /// </summary>
    public static int FenceClearance { get; private set; } = 10;

    /// <summary>
    /// Width of the fence gate opening in blocks.
    /// </summary>
    public static int GateWidth { get; private set; } = 5;

    /// <summary>
    /// The active neighborhood plan, or null if neighborhoods are not enabled.
    /// Set by <see cref="PlanNeighborhoods"/> after resource discovery.
    /// </summary>
    public static NeighborhoodPlan? ActiveNeighborhoodPlan { get; private set; }

    /// <summary>Gap between zone quadrants in blocks.</summary>
    public const int ZoneGap = 20;

    /// <summary>Dashboard wall X position (west of village).</summary>
    public const int DashboardX = BaseX - 15;

    /// <summary>Number of time columns on the dashboard.</summary>
    public const int DashboardColumns = 10;



    /// <summary>
    /// Classifies a resource type string into a <see cref="ResourceCategory"/> for zone assignment.
    /// Uses the same classification logic as <see cref="StructureBuilder.GetStructureType"/>.
    /// </summary>
    public static ResourceCategory GetResourceCategory(string resourceType)
    {
        var lower = resourceType.ToLowerInvariant();

        // Database check (same as StructureBuilder.IsDatabaseResource)
        if (lower.Contains("postgres") || lower.Contains("redis") || lower.Contains("sqlserver")
            || lower.Contains("sql-server") || lower.Contains("mongodb") || lower.Contains("mysql")
            || lower.Contains("mariadb") || lower.Contains("cosmosdb") || lower.Contains("oracle")
            || lower.Contains("sqlite") || lower.Contains("rabbitmq"))
            return ResourceCategory.ContainerOrDatabase;

        // Azure check (same as StructureBuilder.IsAzureResource)
        if (lower.Contains("azure") || lower.Contains("cosmos") || lower.Contains("servicebus")
            || lower.Contains("eventhub") || lower.Contains("keyvault")
            || lower.Contains("appconfiguration") || lower.Contains("signalr")
            || lower.Contains("storage"))
            return ResourceCategory.Azure;

        // Executable check (same as StructureBuilder.IsExecutableResource)
        if (lower.Contains("executable") || lower.Contains("pythonapp")
            || lower.Contains("nodeapp") || lower.Contains("javascriptapp")
            || lower.Contains("javaapp") || lower.Contains("springapp"))
            return ResourceCategory.Executable;

        // Project type
        if (lower == "project")
            return ResourceCategory.DotNetProject;

        // Container (not database)
        if (lower == "container")
            return ResourceCategory.ContainerOrDatabase;

        return ResourceCategory.Other;
    }

    /// <summary>
    /// Creates a neighborhood plan that groups resources by type into a 2×2 quadrant layout.
    /// Sets <see cref="ActiveNeighborhoodPlan"/> for use by all services.
    /// Each zone uses a 2-column sub-grid with dependency ordering within the zone.
    /// Quadrants: NW=DotNetProject, NE=Azure, SW=ContainerOrDatabase, SE=Executable.
    /// </summary>
    public static NeighborhoodPlan PlanNeighborhoods(IReadOnlyDictionary<string, ResourceInfo> resources)
    {
        // Group resources by category
        var groups = new Dictionary<ResourceCategory, List<string>>();
        foreach (ResourceCategory cat in Enum.GetValues(typeof(ResourceCategory)))
            groups[cat] = new List<string>();

        foreach (var (name, info) in resources)
        {
            var category = GetResourceCategory(info.Type);
            groups[category].Add(name);
        }

        // Dependency-order within each group
        foreach (var (cat, names) in groups)
        {
            if (names.Count <= 1) continue;
            var subset = new Dictionary<string, ResourceInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in names)
                if (resources.TryGetValue(n, out var info))
                    subset[n] = info;
            var ordered = ReorderByDependency(subset);
            groups[cat] = ordered;
        }

        // Merge "Other" into Executable zone
        if (groups[ResourceCategory.Other].Count > 0)
        {
            groups[ResourceCategory.Executable].AddRange(groups[ResourceCategory.Other]);
            groups[ResourceCategory.Other].Clear();
        }

        // 2×2 quadrant layout: NW=DotNet, NE=Azure, SW=Container/DB, SE=Executable
        var nwCount = groups[ResourceCategory.DotNetProject].Count;
        var neCount = groups[ResourceCategory.Azure].Count;
        var swCount = groups[ResourceCategory.ContainerOrDatabase].Count;
        var seCount = groups[ResourceCategory.Executable].Count;

        // Zone extents (width/depth occupied by buildings in each zone's 2-column sub-grid)
        var nwExtentX = ZoneExtentX(nwCount);
        var nwExtentZ = ZoneExtentZ(nwCount);
        var neExtentZ = ZoneExtentZ(neCount);
        var swExtentX = ZoneExtentX(swCount);

        // X split: east zones start after the widest west zone + gap
        var westMaxExtentX = Math.Max(nwExtentX, swExtentX);
        var hasWestBuildings = nwCount > 0 || swCount > 0;
        var eastOriginX = hasWestBuildings ? BaseX + westMaxExtentX + ZoneGap : BaseX;

        // Z split: south zones start after the deepest north zone + gap
        var northMaxExtentZ = Math.Max(nwExtentZ, neExtentZ);
        var hasNorthBuildings = nwCount > 0 || neCount > 0;
        var southOriginZ = hasNorthBuildings ? BaseZ + northMaxExtentZ + ZoneGap : BaseZ;

        var neighborhoods = new List<Neighborhood>();
        var positions = new Dictionary<string, (int x, int z)>(StringComparer.OrdinalIgnoreCase);

        void AddZone(ResourceCategory cat, int originX, int originZ, List<string> names)
        {
            if (names.Count == 0) return;
            var hood = new Neighborhood(cat, originX, originZ, names);
            neighborhoods.Add(hood);
            for (var i = 0; i < names.Count; i++)
            {
                var col = i % Columns;
                var row = i / Columns;
                var x = originX + col * Spacing;
                var z = originZ + row * Spacing;
                positions[names[i]] = (x, z);
            }
        }

        AddZone(ResourceCategory.DotNetProject, BaseX, BaseZ, groups[ResourceCategory.DotNetProject]);
        AddZone(ResourceCategory.Azure, eastOriginX, BaseZ, groups[ResourceCategory.Azure]);
        AddZone(ResourceCategory.ContainerOrDatabase, BaseX, southOriginZ, groups[ResourceCategory.ContainerOrDatabase]);
        AddZone(ResourceCategory.Executable, eastOriginX, southOriginZ, groups[ResourceCategory.Executable]);

        var plan = new NeighborhoodPlan(neighborhoods, positions);
        ActiveNeighborhoodPlan = plan;
        return plan;
    }

    /// <summary>X extent of a zone's 2-column sub-grid in blocks.</summary>
    private static int ZoneExtentX(int count) =>
        count == 0 ? 0 : (Math.Min(count, Columns) - 1) * Spacing + StructureSize;

    /// <summary>Z extent of a zone's 2-column sub-grid in blocks.</summary>
    private static int ZoneExtentZ(int count) =>
        count == 0 ? 0 : ((count - 1) / Columns) * Spacing + StructureSize;

    /// <summary>
    /// Gets the origin (southwest corner) position for a structure at the given resource index.
    /// 2-column grid layout: buildings wrap into rows of 2 on a single Y-plane.
    /// 
    /// <para>Calculation: col = index % Columns, row = index / Columns</para>
    /// <para>X = BaseX + col × Spacing</para>
    /// <para>Z = BaseZ + row × Spacing</para>
    /// <para>Y is always SurfaceY + 1 (one block above detected terrain height)</para>
    /// </summary>
    public static (int x, int y, int z) GetStructureOrigin(int index)
    {
        var col = index % Columns;
        var row = index / Columns;
        var x = BaseX + col * Spacing;
        var y = SurfaceY + 1;
        var z = BaseZ + row * Spacing;
        return (x, y, z);
    }

    /// <summary>
    /// Gets the origin position for a named resource using the active neighborhood plan.
    /// Falls back to index-based lookup if no plan is active.
    /// </summary>
    public static (int x, int y, int z) GetStructureOrigin(string resourceName, int fallbackIndex)
    {
        if (ActiveNeighborhoodPlan is not null
            && ActiveNeighborhoodPlan.ResourcePositions.TryGetValue(resourceName, out var pos))
        {
            return (pos.x, SurfaceY + 1, pos.z);
        }
        return GetStructureOrigin(fallbackIndex);
    }

    /// <summary>
    /// Gets the center position of a structure at the given resource index.
    /// Center is offset by half the structure size from the origin.
    /// </summary>
    public static (int x, int y, int z) GetStructureCenter(int index)
    {
        var (ox, oy, oz) = GetStructureOrigin(index);
        var half = StructureSize / 2;
        return (ox + half, oy, oz + half);
    }

    /// <summary>
    /// Gets the center position of a named resource using the active neighborhood plan.
    /// </summary>
    public static (int x, int y, int z) GetStructureCenter(string resourceName, int fallbackIndex)
    {
        var (ox, oy, oz) = GetStructureOrigin(resourceName, fallbackIndex);
        var half = StructureSize / 2;
        return (ox + half, oy, oz + half);
    }

    /// <summary>
    /// Gets a position above the structure roof, suitable for particles, mobs, etc.
    /// </summary>
    public static (int x, int y, int z) GetAboveStructure(int index, int heightAboveBase = 10)
    {
        var (cx, _, cz) = GetStructureCenter(index);
        return (cx, SurfaceY + heightAboveBase, cz);
    }

    /// <summary>
    /// Gets a position above a named resource's structure roof.
    /// </summary>
    public static (int x, int y, int z) GetAboveStructure(string resourceName, int fallbackIndex, int heightAboveBase = 10)
    {
        var (cx, _, cz) = GetStructureCenter(resourceName, fallbackIndex);
        return (cx, SurfaceY + heightAboveBase, cz);
    }

    /// <summary>Canal channel width in blocks (wall + 3 water + wall = 5 total).</summary>
    public const int CanalTotalWidth = 5;

    /// <summary>Canal water width (3 blocks — boats need 2+ to float without wall friction).</summary>
    public const int CanalWaterWidth = 3;

    /// <summary>Canal depth below surface (2 blocks deep).</summary>
    public const int CanalDepth = 2;

    /// <summary>Canal water Y level (one block below grass surface).</summary>
    public static int CanalY => SurfaceY - 1;

    /// <summary>Lake dimensions.</summary>
    public const int LakeWidth = 80; // As wide as the entire town for creeper boat landings
    public const int LakeLength = 40; // Deep and impressive
    public const int LakeBlockDepth = 3;

    /// <summary>Gap between last structure row and lake edge.</summary>
    public const int LakeGap = 20;

    /// <summary>
    /// Gets the rail entrance position centered in front of the building entrance.
    /// Positioned at the center X of the structure, one block south of the front wall (Z - 1),
    /// at surface level + 1 (on top of ground).
    /// </summary>
    public static (int x, int y, int z) GetRailEntrance(int index)
    {
        var (ox, _, oz) = GetStructureOrigin(index);
        var half = StructureSize / 2;
        return (ox + half, SurfaceY + 1, oz - 1);
    }

    /// <summary>
    /// Gets the rail entrance position for a named resource using the active neighborhood plan.
    /// </summary>
    public static (int x, int y, int z) GetRailEntrance(string resourceName, int fallbackIndex)
    {
        var (ox, _, oz) = GetStructureOrigin(resourceName, fallbackIndex);
        var half = StructureSize / 2;
        return (ox + half, SurfaceY + 1, oz - 1);
    }

    /// <summary>
    /// Gets the bounding box of the entire village area (all structure footprints).
    /// Returns (minX, minZ, maxX, maxZ) covering all structures in the grid.
    /// </summary>
    public static (int minX, int minZ, int maxX, int maxZ) GetVillageBounds(int resourceCount)
    {
        // When neighborhood plan is active, compute bounds from all zone positions
        if (ActiveNeighborhoodPlan is not null && ActiveNeighborhoodPlan.ResourcePositions.Count > 0)
        {
            return GetVillageBoundsFromPlan(ActiveNeighborhoodPlan);
        }

        // 2-column grid layout: buildings arranged in Columns wide × N rows deep
        var cols = Math.Min(resourceCount, Columns);
        var rows = (resourceCount + Columns - 1) / Columns;
        var minX = BaseX;
        var minZ = BaseZ;
        var maxX = BaseX + (cols - 1) * Spacing + StructureSize - 1;
        var maxZ = BaseZ + (rows - 1) * Spacing + StructureSize - 1;

        return (minX, minZ, maxX, maxZ);
    }

    /// <summary>
    /// Computes village bounds from a neighborhood plan by iterating all resource positions.
    /// </summary>
    private static (int minX, int minZ, int maxX, int maxZ) GetVillageBoundsFromPlan(NeighborhoodPlan plan)
    {
        var minX = int.MaxValue;
        var minZ = int.MaxValue;
        var maxX = int.MinValue;
        var maxZ = int.MinValue;
        foreach (var (_, pos) in plan.ResourcePositions)
        {
            minX = Math.Min(minX, pos.x);
            minZ = Math.Min(minZ, pos.z);
            maxX = Math.Max(maxX, pos.x + StructureSize - 1);
            maxZ = Math.Max(maxZ, pos.z + StructureSize - 1);
        }
        return (minX, minZ, maxX, maxZ);
    }

    /// <summary>
    /// Gets the fence perimeter coordinates with <see cref="FenceClearance"/> gap from village bounds.
    /// Returns (minX, minZ, maxX, maxZ) for fence placement at BaseY (grass surface level).
    /// 
    /// <para>The clearance gap gives horses room to roam between buildings and the fence.</para>
    /// <para>Fence is placed at BaseY (same level as grass), not elevated.</para>
    /// </summary>
    public static (int minX, int minZ, int maxX, int maxZ) GetFencePerimeter(int resourceCount)
    {
        var (minX, minZ, maxX, maxZ) = GetVillageBounds(resourceCount);
        return (minX - FenceClearance, minZ - FenceClearance, maxX + FenceClearance, maxZ + FenceClearance);
    }

    /// <summary>
    /// Gets the canal entrance position for a resource (west side of building).
    /// Canal runs from building toward the trunk on the west side of town.
    /// </summary>
    public static (int x, int y, int z) GetCanalEntrance(int index)
    {
        var (ox, _, oz) = GetStructureOrigin(index);
        return (ox - 2, CanalY, oz + StructureSize / 2);
    }

    /// <summary>
    /// Gets the canal entrance position for a named resource using the active neighborhood plan.
    /// </summary>
    public static (int x, int y, int z) GetCanalEntrance(string resourceName, int fallbackIndex)
    {
        var (ox, _, oz) = GetStructureOrigin(resourceName, fallbackIndex);
        return (ox - 2, CanalY, oz + StructureSize / 2);
    }

    /// <summary>
    /// Gets the lake's northwest corner position.
    /// Lake spans the full width of the village (or wider), placed beyond the last row.
    /// For the new canal system, the lake needs to be as wide as the entire town.
    /// </summary>
    public static (int x, int y, int z) GetLakePosition(int resourceCount)
    {
        var (minX, _, maxX, maxZ) = GetVillageBounds(resourceCount);
        // Center the lake on the village X-axis
        var centerX = (minX + maxX) / 2;
        return (centerX - LakeWidth / 2, SurfaceY - LakeBlockDepth, maxZ + LakeGap);
    }

    /// <summary>
    /// Gets all building footprint bounding boxes for collision detection.
    /// Each box is (minX, minZ, maxX, maxZ) at surface level.
    /// Used by canal and rail routing to avoid cutting through structures.
    /// </summary>
    public static List<(int minX, int minZ, int maxX, int maxZ)> GetAllBuildingFootprints(
        IReadOnlyList<string> orderedNames)
    {
        var footprints = new List<(int, int, int, int)>();
        for (var i = 0; i < orderedNames.Count; i++)
        {
            var (ox, _, oz) = GetStructureOrigin(orderedNames[i], i);
            // Add 1-block buffer around each structure for walls/decorations
            footprints.Add((ox - 1, oz - 2, ox + StructureSize, oz + StructureSize));
        }
        return footprints;
    }

    /// <summary>
    /// Checks if a horizontal segment (at fixed Z, from minX to maxX) intersects any building footprint.
    /// </summary>
    public static bool SegmentIntersectsBuilding(
        int z, int zWidth,
        int minX, int maxX,
        IReadOnlyList<(int minX, int minZ, int maxX, int maxZ)> footprints,
        int excludeIndex = -1)
    {
        var segZMin = z - zWidth / 2 - 1;
        var segZMax = z + zWidth / 2 + 1;
        for (var i = 0; i < footprints.Count; i++)
        {
            if (i == excludeIndex) continue;
            var fp = footprints[i];
            if (maxX >= fp.minX && minX <= fp.maxX && segZMax >= fp.minZ && segZMin <= fp.maxZ)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Reorders resource names so that dependencies are placed adjacent in the village grid.
    /// Parents appear before children, and resources sharing dependencies are grouped together.
    /// Uses topological sort with BFS ordering.
    /// </summary>
    /// <param name="resources">The discovered resources with their dependency information.</param>
    /// <returns>An ordered list of resource names for grid placement.</returns>
    public static List<string> ReorderByDependency(IReadOnlyDictionary<string, ResourceInfo> resources)
    {
        if (resources.Count == 0)
            return new List<string>();

        var allNames = new HashSet<string>(resources.Keys, StringComparer.OrdinalIgnoreCase);

        // Build in-degree map and adjacency list (parent → children)
        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var childrenOf = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in allNames)
        {
            inDegree[name] = 0;
            childrenOf[name] = new List<string>();
        }

        foreach (var (name, info) in resources)
        {
            foreach (var dep in info.Dependencies)
            {
                var depLower = dep.ToLowerInvariant();
                if (allNames.Contains(depLower))
                {
                    childrenOf[depLower].Add(name);
                    inDegree[name]++;
                }
            }
        }

        // BFS topological sort — roots (no dependencies) first
        var queue = new Queue<string>();
        foreach (var (name, degree) in inDegree)
        {
            if (degree == 0)
                queue.Enqueue(name);
        }

        var ordered = new List<string>(resources.Count);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            ordered.Add(current);

            foreach (var child in childrenOf[current])
            {
                inDegree[child]--;
                if (inDegree[child] == 0)
                    queue.Enqueue(child);
            }
        }

        // Add any remaining resources (cycles or missing deps)
        foreach (var name in allNames)
        {
            if (!ordered.Contains(name))
                ordered.Add(name);
        }

        return ordered;
    }
}
