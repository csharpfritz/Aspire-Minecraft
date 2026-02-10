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
}
