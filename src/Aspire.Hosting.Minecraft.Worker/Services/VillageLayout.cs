namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Centralizes the 2×N grid layout coordinates for the resource village.
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
/// <item>2 columns (Columns=2), infinite rows</item>
/// <item>Spacing=24 blocks center-to-center (standard) or 36 blocks (grand, via <see cref="ConfigureGrandLayout"/>)</item>
/// <item>Each structure footprint: 7×7 blocks by default (configurable via <see cref="ConfigureGrandLayout"/>)</item>
/// <item>Index 0 → (10, -60, 0), Index 1 → (34, -60, 0), Index 2 → (10, -60, 24), etc.</item>
/// </list>
/// 
/// <para><b>Z-Coordinate Conventions:</b></para>
/// <list type="bullet">
/// <item>Front wall (entrance): Always on Z-min side (south-facing)</item>
/// <item>Hollow structures (watchtower): Inner 5×5 box starts at origin+(1,1,1), so front wall is at z+1</item>
/// <item>Solid structures (warehouse/workshop/cottage): Front wall is at z (origin edge)</item>
/// <item>Doors must clear at the actual wall Z coordinate, not origin Z</item>
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
    public static int Spacing { get; private set; } = 24;

    /// <summary>Number of columns in the village grid.</summary>
    public const int Columns = 2;

    /// <summary>Structure footprint width/depth. Default 7×7 (standard), 15×15 (grand).</summary>
    public static int StructureSize { get; private set; } = 7;

    /// <summary>
    /// Clearance (in blocks) between village bounds and fence perimeter.
    /// Default 10 (standard and grand).
    /// </summary>
    public static int FenceClearance { get; private set; } = 10;

    /// <summary>
    /// Width of the fence gate opening in blocks.
    /// Default 3 (standard), 5 (grand — wider for larger village entrance).
    /// </summary>
    public static int GateWidth { get; private set; } = 3;

    /// <summary>
    /// Whether the Grand Village layout is active.
    /// </summary>
    public static bool IsGrandLayout { get; private set; }

    /// <summary>Dashboard wall X position (west of village).</summary>
    public const int DashboardX = BaseX - 15;

    /// <summary>Number of time columns on the dashboard.</summary>
    public const int DashboardColumns = 10;

    /// <summary>
    /// Switches to Grand Village layout with larger structures, wider spacing, and tighter fence clearance.
    /// Must be called once at startup before any structure placement.
    /// Sets StructureSize=15, Spacing=36, and FenceClearance=10. Spacing increases from 24 to 36 to accommodate canals.
    /// </summary>
    public static void ConfigureGrandLayout()
    {
        StructureSize = 15;
        Spacing = 36;
        FenceClearance = 10;
        GateWidth = 5;
        IsGrandLayout = true;
    }

    /// <summary>
    /// Resets layout properties to their default (standard) values.
    /// Intended for test isolation only.
    /// </summary>
    internal static void ResetLayout()
    {
        Spacing = 24;
        StructureSize = 7;
        FenceClearance = 10;
        GateWidth = 3;
        IsGrandLayout = false;
    }

    /// <summary>
    /// Gets the origin (southwest corner) position for a structure at the given resource index.
    /// Layout is a 2-column grid: index 0 → col 0 row 0, index 1 → col 1 row 0, index 2 → col 0 row 1, etc.
    /// 
    /// <para>Calculation: col = index % 2, row = index / 2</para>
    /// <para>X = BaseX + (col × Spacing) → 10, 34, 10, 34, ...</para>
    /// <para>Z = BaseZ + (row × Spacing) → 0, 0, 24, 24, 48, 48, ...</para>
    /// <para>Y is always SurfaceY + 1 (one block above detected terrain height)</para>
    /// </summary>
    public static (int x, int y, int z) GetStructureOrigin(int index)
    {
        var col = index % Columns;
        var row = index / Columns;
        var x = BaseX + (col * Spacing);
        var y = SurfaceY + 1;
        var z = BaseZ + (row * Spacing);
        return (x, y, z);
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
    /// Gets a position above the structure roof, suitable for particles, mobs, etc.
    /// </summary>
    public static (int x, int y, int z) GetAboveStructure(int index, int heightAboveBase = 10)
    {
        var (cx, _, cz) = GetStructureCenter(index);
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
    public const int LakeWidth = 20;
    public const int LakeLength = 12;
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
    /// Gets the bounding box of the entire village area (all structure footprints).
    /// Returns (minX, minZ, maxX, maxZ) covering all structures in the grid.
    /// </summary>
    public static (int minX, int minZ, int maxX, int maxZ) GetVillageBounds(int resourceCount)
    {
        var rows = (resourceCount + Columns - 1) / Columns;
        var cols = resourceCount >= Columns ? Columns : resourceCount;

        var minX = BaseX;
        var minZ = BaseZ;
        var maxX = BaseX + ((cols - 1) * Spacing) + StructureSize - 1;
        var maxZ = BaseZ + ((rows - 1) * Spacing) + StructureSize - 1;

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
    /// Gets the canal entrance position for a resource (east side of building).
    /// Canal runs from building toward the lake at Z-max.
    /// </summary>
    public static (int x, int y, int z) GetCanalEntrance(int index)
    {
        var (ox, _, oz) = GetStructureOrigin(index);
        return (ox + StructureSize + 2, CanalY, oz + StructureSize / 2);
    }

    /// <summary>
    /// Gets the lake's northwest corner position.
    /// Lake is centered on the village's X-axis, placed beyond the last row.
    /// </summary>
    public static (int x, int y, int z) GetLakePosition(int resourceCount)
    {
        var (minX, _, maxX, maxZ) = GetVillageBounds(resourceCount);
        var centerX = (minX + maxX) / 2;
        return (centerX - LakeWidth / 2, SurfaceY - LakeBlockDepth, maxZ + LakeGap);
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
