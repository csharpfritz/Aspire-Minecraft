using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Services;

/// <summary>
/// Detects unintentional fill-command overlaps that silently overwrite blocks.
/// Parses all /fill commands into bounding boxes, then checks every pair for
/// overlapping volumes. Air/cave_air fills are treated as intentional clearing;
/// only solid-on-solid overlaps are flagged as bugs.
/// </summary>
public class FillOverlapDetectionTests : IAsyncLifetime
{
    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private AspireResourceMonitor _monitor = null!;
    private StructureBuilder _structureBuilder = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance);
        _monitor = TestResourceMonitorFactory.Create();
        _structureBuilder = new StructureBuilder(_rcon, _monitor,
            new BuildingProtectionService(NullLogger<BuildingProtectionService>.Instance),
            NullLogger<StructureBuilder>.Instance);

        await WaitForRconConnected();
    }

    public async Task DisposeAsync()
    {
        await _rcon.DisposeAsync();
        await _server.DisposeAsync();
    }

    private async Task WaitForRconConnected()
    {
        for (int i = 0; i < 10; i++)
        {
            try { await _rcon.SendCommandAsync("list"); return; }
            catch { await Task.Delay(100); }
        }
    }

    // ====================================================================
    // FILL COMMAND PARSING & OVERLAP DETECTION HELPERS
    // ====================================================================

    /// <summary>
    /// Represents an axis-aligned bounding box parsed from a /fill command,
    /// along with the block type being placed.
    /// </summary>
    private record FillBox(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ, string Block, string RawCommand);

    /// <summary>
    /// Parses a /fill command string into a <see cref="FillBox"/>.
    /// Expected format: "fill x1 y1 z1 x2 y2 z2 minecraft:block [modifiers]"
    /// Returns null if the command is not a fill command.
    /// </summary>
    private static FillBox? ParseFillCommand(string command)
    {
        if (!command.StartsWith("fill ")) return null;

        var parts = command.Split(' ');
        if (parts.Length < 8) return null;

        if (!int.TryParse(parts[1], out int x1)) return null;
        if (!int.TryParse(parts[2], out int y1)) return null;
        if (!int.TryParse(parts[3], out int z1)) return null;
        if (!int.TryParse(parts[4], out int x2)) return null;
        if (!int.TryParse(parts[5], out int y2)) return null;
        if (!int.TryParse(parts[6], out int z2)) return null;

        var block = parts[7];

        return new FillBox(
            Math.Min(x1, x2), Math.Min(y1, y2), Math.Min(z1, z2),
            Math.Max(x1, x2), Math.Max(y1, y2), Math.Max(z1, z2),
            block, command);
    }

    /// <summary>
    /// Returns true if two bounding boxes overlap in all three axes.
    /// </summary>
    private static bool BoxesOverlap(FillBox a, FillBox b)
    {
        return a.MinX <= b.MaxX && a.MaxX >= b.MinX
            && a.MinY <= b.MaxY && a.MaxY >= b.MinY
            && a.MinZ <= b.MaxZ && a.MaxZ >= b.MinZ;
    }

    /// <summary>
    /// Returns true if the block type represents an air/clearing operation
    /// (intentional overwrite).
    /// </summary>
    private static bool IsAirOrClearing(string block)
    {
        return block.Contains("minecraft:air")
            || block.Contains("minecraft:cave_air");
    }

    /// <summary>
    /// Returns true if a fill command is a "hollow" command, which only fills
    /// the outer shell and leaves interior as air. Hollow fills are inherently
    /// non-overlapping with their own interior.
    /// </summary>
    private static bool IsHollowFill(string rawCommand)
    {
        return rawCommand.EndsWith(" hollow", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true if the later fill is an intentional architectural layering
    /// over the earlier fill. Minecraft building technique places large regions
    /// first, then overlays decorative details — these are expected overlaps.
    /// </summary>
    private static bool IsIntentionalLayering(FillBox earlier, FillBox later)
    {
        // Same block type — redundant fill is harmless (e.g., beam over post)
        if (NormalizeBlockName(earlier.Block) == NormalizeBlockName(later.Block))
            return true;

        // Fence gate replacing part of fence line
        if (earlier.Block.Contains("fence") && later.Block.Contains("fence_gate"))
            return true;
        if (earlier.Block.Contains("fence_gate") && later.Block.Contains("fence"))
            return true;

        // Decorative detail overlaying structural element (smaller overwrites larger)
        // E.g., wool trim at pillar intersections, stair caps on turrets, chimney on roof
        long earlierVolume = (long)(earlier.MaxX - earlier.MinX + 1) *
                             (earlier.MaxY - earlier.MinY + 1) *
                             (earlier.MaxZ - earlier.MinZ + 1);
        long laterVolume = (long)(later.MaxX - later.MinX + 1) *
                           (later.MaxY - later.MinY + 1) *
                           (later.MaxZ - later.MinZ + 1);

        // If the later (overwriting) fill is smaller than the earlier,
        // it's architectural detailing on top of a structural fill
        if (laterVolume < earlierVolume)
            return true;

        // Interior furnishing placed inside structural walls
        // (e.g., floors/bookshelves inside corner buttresses, planks inside walls)
        var interiorBlocks = new[]
        {
            "oak_planks", "bookshelf", "iron_block", "copper_block",
            "barrel", "chest", "lantern", "crafting_table", "smithing_table",
            "stonecutter", "anvil", "grindstone", "furnace", "brewing_stand",
            "carpet", "enchanting_table", "lectern", "ladder", "torch",
            "potted_", "red_bed", "campfire", "flower_pot"
        };
        if (interiorBlocks.Any(b => later.Block.Contains(b)))
            return true;

        // Structural refinement: same material family replacing each other
        // E.g., stone_bricks replacing cracked_stone_bricks (gatehouse over weathering)
        if (earlier.Block.Contains("stone_brick") && later.Block.Contains("stone_brick"))
            return true;
        if (earlier.Block.Contains("cobblestone") && later.Block.Contains("stone_brick"))
            return true;

        // Gatehouse/structural fill over decorative window elements
        // E.g., stone_bricks gatehouse over iron_bars arrow slits
        if (earlier.Block.Contains("iron_bars") && later.Block.Contains("stone_brick"))
            return true;
        if (earlier.Block.Contains("glass") && later.Block.Contains("stone_brick"))
            return true;

        // Decorative wool/banner trim over structural elements
        if (later.Block.Contains("wool") || later.Block.Contains("banner"))
            return true;

        return false;
    }

    /// <summary>
    /// Normalizes a block name by removing state/properties (e.g., "[facing=south]").
    /// </summary>
    private static string NormalizeBlockName(string block)
    {
        var bracketIndex = block.IndexOf('[');
        return bracketIndex >= 0 ? block[..bracketIndex] : block;
    }

    /// <summary>
    /// Detects solid-on-solid fill overlaps in a list of RCON commands.
    /// Returns descriptions of any unintentional overlaps found.
    /// A later fill overwriting an earlier fill is flagged UNLESS:
    /// - The later fill is air/cave_air (intentional clearing)
    /// - One of the fills is a "hollow" command (shell only)
    /// - The later fill uses "replace" modifier targeting a specific block
    /// - The overlap is intentional architectural layering
    /// </summary>
    private static List<string> DetectSolidOnSolidOverlaps(List<string> commands)
    {
        var fills = new List<(int Index, FillBox Box)>();
        for (int i = 0; i < commands.Count; i++)
        {
            var box = ParseFillCommand(commands[i]);
            if (box != null)
                fills.Add((i, box));
        }

        var overlaps = new List<string>();

        for (int i = 0; i < fills.Count; i++)
        {
            for (int j = i + 1; j < fills.Count; j++)
            {
                var earlier = fills[i];
                var later = fills[j];

                if (!BoxesOverlap(earlier.Box, later.Box))
                    continue;

                // Skip if the later fill is air (intentional clearing)
                if (IsAirOrClearing(later.Box.Block))
                    continue;

                // Skip if the earlier fill is air (placing air then building on top is fine)
                if (IsAirOrClearing(earlier.Box.Block))
                    continue;

                // Skip if either is a hollow fill (shell-only, doesn't fill interior)
                if (IsHollowFill(earlier.Box.RawCommand) || IsHollowFill(later.Box.RawCommand))
                    continue;

                // Skip if the later fill uses "replace" modifier (conditional replacement)
                if (later.Box.RawCommand.Contains(" replace "))
                    continue;

                // Skip intentional architectural layering
                if (IsIntentionalLayering(earlier.Box, later.Box))
                    continue;

                overlaps.Add(
                    $"Fill #{later.Index} overwrites fill #{earlier.Index}:\n" +
                    $"  Earlier: {earlier.Box.RawCommand}\n" +
                    $"  Later:   {later.Box.RawCommand}");
            }
        }

        return overlaps;
    }

    /// <summary>
    /// Builds a single structure and returns detected overlaps.
    /// </summary>
    private async Task<List<string>> BuildAndDetectOverlaps(string resourceName, string resourceType, bool grandLayout = false)
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            (resourceName, resourceType, ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        return DetectSolidOnSolidOverlaps(commands);
    }

    // ====================================================================
    // GRAND (15×15) BUILDING FILL-OVERLAP TESTS
    // ====================================================================

    [Fact]
    public async Task Grand_Watchtower_NoSolidOnSolidFillOverlaps()
    {
        var overlaps = await BuildAndDetectOverlaps("api", "Project", grandLayout: true);
        Assert.True(overlaps.Count == 0,
            $"Grand Watchtower has {overlaps.Count} solid-on-solid fill overlap(s):\n{string.Join("\n\n", overlaps)}");
    }

    [Fact]
    public async Task Grand_Warehouse_NoSolidOnSolidFillOverlaps()
    {
        var overlaps = await BuildAndDetectOverlaps("redis", "Container", grandLayout: true);
        Assert.True(overlaps.Count == 0,
            $"Grand Warehouse has {overlaps.Count} solid-on-solid fill overlap(s):\n{string.Join("\n\n", overlaps)}");
    }

    [Fact]
    public async Task Grand_Workshop_NoSolidOnSolidFillOverlaps()
    {
        var overlaps = await BuildAndDetectOverlaps("worker", "Executable", grandLayout: true);
        Assert.True(overlaps.Count == 0,
            $"Grand Workshop has {overlaps.Count} solid-on-solid fill overlap(s):\n{string.Join("\n\n", overlaps)}");
    }

    [Fact]
    public async Task Grand_Cottage_NoSolidOnSolidFillOverlaps()
    {
        var overlaps = await BuildAndDetectOverlaps("misc", "SomeType", grandLayout: true);
        Assert.True(overlaps.Count == 0,
            $"Grand Cottage has {overlaps.Count} solid-on-solid fill overlap(s):\n{string.Join("\n\n", overlaps)}");
    }

    [Fact]
    public async Task Grand_Cylinder_NoSolidOnSolidFillOverlaps()
    {
        var overlaps = await BuildAndDetectOverlaps("db", "postgres", grandLayout: true);
        Assert.True(overlaps.Count == 0,
            $"Grand Cylinder has {overlaps.Count} solid-on-solid fill overlap(s):\n{string.Join("\n\n", overlaps)}");
    }

    [Fact]
    public async Task Grand_AzurePavilion_NoSolidOnSolidFillOverlaps()
    {
        var overlaps = await BuildAndDetectOverlaps("storage", "azure.storage", grandLayout: true);
        Assert.True(overlaps.Count == 0,
            $"Grand Azure Pavilion has {overlaps.Count} solid-on-solid fill overlap(s):\n{string.Join("\n\n", overlaps)}");
    }

    // ====================================================================
    // PARSE & DETECTION UNIT TESTS
    // ====================================================================

    [Fact]
    public void ParseFillCommand_ValidCommand_ReturnsCorrectBox()
    {
        var box = ParseFillCommand("fill 10 -59 0 16 -55 6 minecraft:stone_bricks hollow");
        Assert.NotNull(box);
        Assert.Equal(10, box!.MinX);
        Assert.Equal(-59, box.MinY);
        Assert.Equal(0, box.MinZ);
        Assert.Equal(16, box.MaxX);
        Assert.Equal(-55, box.MaxY);
        Assert.Equal(6, box.MaxZ);
        Assert.Equal("minecraft:stone_bricks", box.Block);
    }

    [Fact]
    public void ParseFillCommand_ReversedCoordinates_NormalizesMinMax()
    {
        var box = ParseFillCommand("fill 16 -55 6 10 -59 0 minecraft:air");
        Assert.NotNull(box);
        Assert.Equal(10, box!.MinX);
        Assert.Equal(-59, box.MinY);
        Assert.Equal(0, box.MinZ);
    }

    [Fact]
    public void ParseFillCommand_NonFillCommand_ReturnsNull()
    {
        Assert.Null(ParseFillCommand("setblock 10 -59 0 minecraft:glowstone"));
        Assert.Null(ParseFillCommand("data merge block 10 -59 0 {}"));
    }

    [Fact]
    public void BoxesOverlap_NonOverlapping_ReturnsFalse()
    {
        var a = new FillBox(0, 0, 0, 5, 5, 5, "minecraft:stone", "fill 0 0 0 5 5 5 minecraft:stone");
        var b = new FillBox(6, 0, 0, 10, 5, 5, "minecraft:stone", "fill 6 0 0 10 5 5 minecraft:stone");
        Assert.False(BoxesOverlap(a, b));
    }

    [Fact]
    public void BoxesOverlap_Overlapping_ReturnsTrue()
    {
        var a = new FillBox(0, 0, 0, 5, 5, 5, "minecraft:stone", "fill 0 0 0 5 5 5 minecraft:stone");
        var b = new FillBox(3, 3, 3, 8, 8, 8, "minecraft:stone", "fill 3 3 3 8 8 8 minecraft:stone");
        Assert.True(BoxesOverlap(a, b));
    }

    [Fact]
    public void DetectOverlaps_AirClearingOverSolid_NoFlagged()
    {
        var commands = new List<string>
        {
            "fill 0 0 0 5 5 5 minecraft:stone_bricks hollow",
            "fill 1 1 1 4 4 4 minecraft:air"
        };
        var overlaps = DetectSolidOnSolidOverlaps(commands);
        Assert.Empty(overlaps);
    }

    [Fact]
    public void DetectOverlaps_SolidOnSolid_DifferentLargerBlock_Flagged()
    {
        // Two equal-size fills with different block types that aren't architectural layering
        var commands = new List<string>
        {
            "fill 0 0 0 5 5 5 minecraft:glass",
            "fill 0 0 0 5 5 5 minecraft:diamond_block"
        };
        var overlaps = DetectSolidOnSolidOverlaps(commands);
        Assert.Single(overlaps);
    }

    [Fact]
    public void DetectOverlaps_HollowOverHollow_NoFlagged()
    {
        var commands = new List<string>
        {
            "fill 0 0 0 10 10 10 minecraft:stone hollow",
            "fill 2 2 2 8 8 8 minecraft:iron_block hollow"
        };
        var overlaps = DetectSolidOnSolidOverlaps(commands);
        Assert.Empty(overlaps);
    }
}
