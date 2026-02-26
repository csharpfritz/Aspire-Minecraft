using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// A 3D axis-aligned bounding box protecting a placed building from modification by other subsystems.
/// </summary>
internal readonly record struct ProtectedRegion(
    int MinX, int MinY, int MinZ,
    int MaxX, int MaxY, int MaxZ,
    string Owner);

/// <summary>
/// Tracks protected building regions so that infrastructure subsystems (canals, rails, etc.)
/// can avoid damaging placed structures. After StructureBuilder places a building, it registers
/// the building's 3D bounding box here. Other services call <see cref="ClipFill"/> to split their
/// fill commands around protected volumes.
/// </summary>
internal sealed class BuildingProtectionService(ILogger<BuildingProtectionService> logger)
{
    private readonly List<ProtectedRegion> _regions = new();

    /// <summary>
    /// All registered protected regions (read-only snapshot for diagnostics).
    /// </summary>
    public IReadOnlyList<ProtectedRegion> Regions => _regions;

    /// <summary>
    /// Register a building's 3D bounding box after it has been constructed.
    /// </summary>
    public void Register(int minX, int minY, int minZ, int maxX, int maxY, int maxZ, string owner)
    {
        _regions.Add(new ProtectedRegion(minX, minY, minZ, maxX, maxY, maxZ, owner));
        logger.LogDebug("Protected region registered for {Owner}: ({MinX},{MinY},{MinZ}) to ({MaxX},{MaxY},{MaxZ})",
            owner, minX, minY, minZ, maxX, maxY, maxZ);
    }

    /// <summary>
    /// Check if a 3D region overlaps any protected building.
    /// </summary>
    public bool Overlaps(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
    {
        foreach (var r in _regions)
        {
            if (maxX >= r.MinX && minX <= r.MaxX &&
                maxY >= r.MinY && minY <= r.MaxY &&
                maxZ >= r.MinZ && minZ <= r.MaxZ)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Clip a fill region against all protected buildings, returning sub-regions that
    /// don't overlap any building. Uses axis-aligned box subtraction — each protected
    /// region can split a fill into up to 6 sub-boxes (left/right/below/above/front/back).
    /// </summary>
    public List<(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)> ClipFill(
        int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
    {
        var regions = new List<(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)>
        {
            (minX, minY, minZ, maxX, maxY, maxZ)
        };

        foreach (var prot in _regions)
        {
            var next = new List<(int, int, int, int, int, int)>();
            foreach (var r in regions)
            {
                next.AddRange(SubtractBox(r, prot));
            }
            regions = next;
        }

        if (regions.Count > 1)
        {
            logger.LogDebug("Fill ({MinX},{MinY},{MinZ})->({MaxX},{MaxY},{MaxZ}) clipped into {Count} sub-regions",
                minX, minY, minZ, maxX, maxY, maxZ, regions.Count);
        }

        return regions;
    }

    /// <summary>
    /// Subtract a protected box from a fill box, returning non-overlapping remainder sub-boxes.
    /// Uses 3-axis slicing: X left/right, then Y below/above (clamped X), then Z front/back (clamped X+Y).
    /// </summary>
    private static IEnumerable<(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)> SubtractBox(
        (int minX, int minY, int minZ, int maxX, int maxY, int maxZ) fill,
        ProtectedRegion prot)
    {
        // No overlap → return original unchanged
        if (fill.maxX < prot.MinX || fill.minX > prot.MaxX ||
            fill.maxY < prot.MinY || fill.minY > prot.MaxY ||
            fill.maxZ < prot.MinZ || fill.minZ > prot.MaxZ)
        {
            yield return fill;
            yield break;
        }

        // X slices: everything to the left and right of the protected region
        if (fill.minX < prot.MinX)
            yield return (fill.minX, fill.minY, fill.minZ, prot.MinX - 1, fill.maxY, fill.maxZ);
        if (fill.maxX > prot.MaxX)
            yield return (prot.MaxX + 1, fill.minY, fill.minZ, fill.maxX, fill.maxY, fill.maxZ);

        // Clamp X to the overlap range for Y and Z slices
        var clampMinX = Math.Max(fill.minX, prot.MinX);
        var clampMaxX = Math.Min(fill.maxX, prot.MaxX);

        // Y slices: everything below and above (within clamped X)
        if (fill.minY < prot.MinY)
            yield return (clampMinX, fill.minY, fill.minZ, clampMaxX, prot.MinY - 1, fill.maxZ);
        if (fill.maxY > prot.MaxY)
            yield return (clampMinX, prot.MaxY + 1, fill.minZ, clampMaxX, fill.maxY, fill.maxZ);

        // Clamp Y to the overlap range for Z slices
        var clampMinY = Math.Max(fill.minY, prot.MinY);
        var clampMaxY = Math.Min(fill.maxY, prot.MaxY);

        // Z slices: everything in front and behind (within clamped X + Y)
        if (fill.minZ < prot.MinZ)
            yield return (clampMinX, clampMinY, fill.minZ, clampMaxX, clampMaxY, prot.MinZ - 1);
        if (fill.maxZ > prot.MaxZ)
            yield return (clampMinX, clampMinY, prot.MaxZ + 1, clampMaxX, clampMaxY, fill.maxZ);
    }
}
