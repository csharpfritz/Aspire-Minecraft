namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Centralizes the 2×N grid layout coordinates for the resource village.
/// All services that place things per-resource should use this to get consistent positions.
/// </summary>
internal static class VillageLayout
{
    /// <summary>Base X coordinate for the village grid.</summary>
    public const int BaseX = 10;

    /// <summary>Surface level Y for superflat world (one above grass at Y=-61).</summary>
    public const int BaseY = -60;

    /// <summary>Base Z coordinate for the village grid.</summary>
    public const int BaseZ = 0;

    /// <summary>Spacing between structure origins (center-to-center) in blocks.</summary>
    public const int Spacing = 10;

    /// <summary>Number of columns in the village grid.</summary>
    public const int Columns = 2;

    /// <summary>Structure footprint width/depth (7×7).</summary>
    public const int StructureSize = 7;

    /// <summary>
    /// Gets the origin (corner) position for a structure at the given resource index.
    /// Layout is a 2-column grid: index 0 → col 0 row 0, index 1 → col 1 row 0, etc.
    /// </summary>
    public static (int x, int y, int z) GetStructureOrigin(int index)
    {
        var col = index % Columns;
        var row = index / Columns;
        var x = BaseX + (col * Spacing);
        var y = BaseY;
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
        return (cx, BaseY + heightAboveBase, cz);
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
    /// Gets the fence perimeter coordinates (1 block outside the village bounds).
    /// Returns (minX, minZ, maxX, maxZ) for fence placement.
    /// </summary>
    public static (int minX, int minZ, int maxX, int maxZ) GetFencePerimeter(int resourceCount)
    {
        var (minX, minZ, maxX, maxZ) = GetVillageBounds(resourceCount);
        return (minX - 1, minZ - 2, maxX + 1, maxZ + 1);
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
