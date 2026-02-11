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
/// <item>Spacing=10 blocks center-to-center between structures</item>
/// <item>Each structure footprint: 7×7 blocks (StructureSize=7)</item>
/// <item>Index 0 → (10, -60, 0), Index 1 → (20, -60, 0), Index 2 → (10, -60, 10), etc.</item>
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
    public const int Spacing = 10;

    /// <summary>Number of columns in the village grid.</summary>
    public const int Columns = 2;

    /// <summary>Structure footprint width/depth (7×7).</summary>
    public const int StructureSize = 7;

    /// <summary>
    /// Gets the origin (southwest corner) position for a structure at the given resource index.
    /// Layout is a 2-column grid: index 0 → col 0 row 0, index 1 → col 1 row 0, index 2 → col 0 row 1, etc.
    /// 
    /// <para>Calculation: col = index % 2, row = index / 2</para>
    /// <para>X = BaseX + (col × Spacing) → 10, 20, 10, 20, ...</para>
    /// <para>Z = BaseZ + (row × Spacing) → 0, 0, 10, 10, 20, 20, ...</para>
    /// <para>Y is always SurfaceY (detected terrain height, or -60 fallback)</para>
    /// </summary>
    public static (int x, int y, int z) GetStructureOrigin(int index)
    {
        var col = index % Columns;
        var row = index / Columns;
        var x = BaseX + (col * Spacing);
        var y = SurfaceY;
        var z = BaseZ + (row * Spacing);
        return (x, y, z);
    }

    /// <summary>
    /// Gets the center position of a structure at the given resource index.
    /// Center is offset by half the structure size (3 blocks in from origin).
    /// </summary>
    public static (int x, int y, int z) GetStructureCenter(int index)
    {
        var (ox, oy, oz) = GetStructureOrigin(index);
        return (ox + 3, oy, oz + 3);
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
    /// Gets the fence perimeter coordinates with 4-block clearance from village bounds.
    /// Returns (minX, minZ, maxX, maxZ) for fence placement at BaseY (grass surface level).
    /// 
    /// <para>The 4-block gap ensures fence doesn't overlap structures and provides walking space.</para>
    /// <para>Fence is placed at BaseY (same level as grass), not elevated.</para>
    /// </summary>
    public static (int minX, int minZ, int maxX, int maxZ) GetFencePerimeter(int resourceCount)
    {
        var (minX, minZ, maxX, maxZ) = GetVillageBounds(resourceCount);
        return (minX - 4, minZ - 4, maxX + 4, maxZ + 4);
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
