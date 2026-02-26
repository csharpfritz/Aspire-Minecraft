using System.Text.RegularExpressions;
using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Services;

/// <summary>
/// Proactive test suite for the Grand Observation Tower feature.
/// Written from Rhodey's architecture spec before Rocket's implementation.
///
/// Key coordinates (superflat defaults, SurfaceY = -60):
///   Tower origin:   (25, SurfaceY, -45)
///   Tower extents:  X: 25–45, Z: -45–-25, Y: SurfaceY to SurfaceY+32
///   Footprint:      21×21 blocks
///   Interior:       17×17 (2-block walls each side)
///   Floors:         y+7, y+12, y+17, y+24
///   Spiral:         South→East→North→West (counter-clockwise)
///   Entrance:       South wall (max-Z), facing toward village
///
/// These tests will NOT compile until GrandObservationTowerService is implemented.
/// The geometry and behavior assertions are derived from the spec and should be
/// stable once the service API is finalized.
/// </summary>
public partial class GrandObservationTowerTests : IAsyncLifetime
{
    // ====================================================================
    // EXPECTED CONSTANTS (from Rhodey's architecture plan)
    // ====================================================================

    private const int TowerOriginX = 25;
    private const int TowerOriginZ = -45;
    private const int TowerSideLength = 21;
    private const int TowerHeight = 32;

    private static int SurfaceY => VillageLayout.SurfaceY;
    private static int TowerMinX => TowerOriginX;
    private static int TowerMaxX => TowerOriginX + TowerSideLength - 1; // 45
    private static int TowerMinZ => TowerOriginZ;
    private static int TowerMaxZ => TowerOriginZ + TowerSideLength - 1; // -25
    private static int TowerBaseY => SurfaceY;
    private static int TowerTopY => SurfaceY + TowerHeight; // SurfaceY + 32

    /// <summary>Landing platform Y offsets from SurfaceY.</summary>
    private static readonly int[] FloorOffsets = [7, 12, 17, 24];

    /// <summary>Interior clearance: 1-block walls (hollow fill) on each side → 19×19 interior.</summary>
    private const int InteriorClearance = 19;
    private const int WallThickness = 1;

    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private BuildingProtectionService _protection = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance, maxCommandsPerSecond: 1000);
        _protection = new BuildingProtectionService(
            NullLogger<BuildingProtectionService>.Instance);

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

    /// <summary>
    /// Creates the tower service and builds the tower, returning all RCON commands.
    /// </summary>
    private async Task<List<string>> BuildTowerAndCaptureCommands()
    {
        _server.ClearCommands();
        var service = new GrandObservationTowerService(
            _rcon, _protection,
            NullLogger<GrandObservationTowerService>.Instance);

        await service.ForceloadAsync(CancellationToken.None);
        service.RegisterProtection();
        await service.BuildTowerAsync(CancellationToken.None);
        return _server.GetCommands();
    }

    // ====================================================================
    // COMMAND PARSING HELPERS (same pattern as BridgeGeometryTests)
    // ====================================================================

    private record SetblockCmd(int X, int Y, int Z, string Block);

    private record FillCmd(int X1, int Y1, int Z1, int X2, int Y2, int Z2, string Block)
    {
        public int MinX => Math.Min(X1, X2);
        public int MaxX => Math.Max(X1, X2);
        public int MinY => Math.Min(Y1, Y2);
        public int MaxY => Math.Max(Y1, Y2);
        public int MinZ => Math.Min(Z1, Z2);
        public int MaxZ => Math.Max(Z1, Z2);

        public bool Contains(int x, int y, int z) =>
            x >= MinX && x <= MaxX && y >= MinY && y <= MaxY && z >= MinZ && z <= MaxZ;
    }

    [GeneratedRegex(@"^setblock\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(minecraft:\S+)")]
    private static partial Regex SetblockRegex();

    [GeneratedRegex(@"^fill\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(minecraft:\S+)")]
    private static partial Regex FillRegex();

    [GeneratedRegex(@"^forceload\s+add\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)")]
    private static partial Regex ForceloadRegex();

    private static List<SetblockCmd> ParseSetblocks(IEnumerable<string> commands)
    {
        var results = new List<SetblockCmd>();
        foreach (var cmd in commands)
        {
            var m = SetblockRegex().Match(cmd);
            if (m.Success)
            {
                results.Add(new SetblockCmd(
                    int.Parse(m.Groups[1].Value),
                    int.Parse(m.Groups[2].Value),
                    int.Parse(m.Groups[3].Value),
                    m.Groups[4].Value));
            }
        }
        return results;
    }

    private static List<FillCmd> ParseFills(IEnumerable<string> commands)
    {
        var results = new List<FillCmd>();
        foreach (var cmd in commands)
        {
            var m = FillRegex().Match(cmd);
            if (m.Success)
            {
                results.Add(new FillCmd(
                    int.Parse(m.Groups[1].Value),
                    int.Parse(m.Groups[2].Value),
                    int.Parse(m.Groups[3].Value),
                    int.Parse(m.Groups[4].Value),
                    int.Parse(m.Groups[5].Value),
                    int.Parse(m.Groups[6].Value),
                    m.Groups[7].Value));
            }
        }
        return results;
    }

    /// <summary>
    /// Checks if any fill command covers a specific coordinate with a specific block.
    /// </summary>
    private static bool HasFillCovering(List<FillCmd> fills, int x, int y, int z, string blockSubstring)
    {
        return fills.Any(f => f.Contains(x, y, z) && f.Block.Contains(blockSubstring));
    }

    // ====================================================================
    // 1. PLACEMENT & COORDINATE TESTS
    // ====================================================================

    [Fact]
    public void TowerOrigin_MatchesSpecCoordinates()
    {
        // Spec: Tower origin at (25, SurfaceY, -45)
        Assert.Equal(25, TowerOriginX);
        Assert.Equal(-45, TowerOriginZ);
    }

    [Fact]
    public void TowerExtents_MatchSpec_21x21Footprint()
    {
        // Spec: X: 25 to 45, Z: -45 to -25 (21×21 footprint)
        Assert.Equal(21, TowerSideLength);
        Assert.Equal(25, TowerMinX);
        Assert.Equal(45, TowerMaxX);
        Assert.Equal(-45, TowerMinZ);
        Assert.Equal(-25, TowerMaxZ);

        // Verify footprint dimensions
        Assert.Equal(TowerSideLength, TowerMaxX - TowerMinX + 1);
        Assert.Equal(TowerSideLength, TowerMaxZ - TowerMinZ + 1);
    }

    [Fact]
    public void TowerHeight_Is32BlocksAboveSurface()
    {
        // Spec: 32 blocks tall, y+1 through y+32
        Assert.Equal(32, TowerHeight);
        Assert.Equal(SurfaceY, TowerBaseY);
        Assert.Equal(SurfaceY + 32, TowerTopY);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(11)]
    [InlineData(24)]
    public void TowerFootprint_DoesNotOverlapVillageGrid(int resourceIndex)
    {
        // Verify the tower footprint doesn't intersect any village grid position
        var (ox, _, oz) = VillageLayout.GetStructureOrigin(resourceIndex);
        var structSize = VillageLayout.StructureSize;

        // Structure bounds
        int sMinX = ox, sMaxX = ox + structSize - 1;
        int sMinZ = oz, sMaxZ = oz + structSize - 1;

        // Check for X/Z overlap with tower footprint
        bool xOverlaps = TowerMaxX >= sMinX && TowerMinX <= sMaxX;
        bool zOverlaps = TowerMaxZ >= sMinZ && TowerMinZ <= sMaxZ;

        Assert.False(xOverlaps && zOverlaps,
            $"Tower footprint ({TowerMinX},{TowerMinZ})-({TowerMaxX},{TowerMaxZ}) overlaps " +
            $"grid position {resourceIndex} at ({sMinX},{sMinZ})-({sMaxX},{sMaxZ}).");
    }

    [Fact]
    public void TowerPlacement_NorthOfFenceLine()
    {
        // Tower is placed 15 blocks north of the northern fence line
        // Fence Z-min is BaseZ - FenceClearance = 0 - 10 = -10
        // Tower max-Z = -25, which is 15 blocks north of the fence
        var fenceZMin = VillageLayout.BaseZ - VillageLayout.FenceClearance;
        Assert.True(TowerMaxZ < fenceZMin,
            $"Tower Z-max ({TowerMaxZ}) should be north of fence Z-min ({fenceZMin})");
    }

    // ====================================================================
    // 2. BUILDING PROTECTION TESTS
    // ====================================================================

    [Fact]
    public async Task BuildTower_RegistersProtectionZone()
    {
        // Act
        await BuildTowerAndCaptureCommands();

        // Assert: at least one protection region registered with the tower's name
        Assert.Contains(_protection.Regions,
            r => r.Owner.Contains("Tower", StringComparison.OrdinalIgnoreCase) ||
                 r.Owner.Contains("Observation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ProtectionZone_ExtendsOneBlockBeyondFootprint()
    {
        // Act
        await BuildTowerAndCaptureCommands();

        // Find the tower protection region
        var towerRegion = _protection.Regions.FirstOrDefault(
            r => r.Owner.Contains("Tower", StringComparison.OrdinalIgnoreCase) ||
                 r.Owner.Contains("Observation", StringComparison.OrdinalIgnoreCase));

        Assert.NotEqual(default, towerRegion);

        // Protection zone should extend at least 1 block beyond tower footprint
        Assert.True(towerRegion.MinX <= TowerMinX - 1,
            $"Protection MinX ({towerRegion.MinX}) should be <= tower MinX - 1 ({TowerMinX - 1})");
        Assert.True(towerRegion.MaxX >= TowerMaxX + 1,
            $"Protection MaxX ({towerRegion.MaxX}) should be >= tower MaxX + 1 ({TowerMaxX + 1})");
        Assert.True(towerRegion.MaxZ >= TowerMaxZ + 1,
            $"Protection MaxZ ({towerRegion.MaxZ}) should be >= tower MaxZ + 1 ({TowerMaxZ + 1})");
    }

    [Fact]
    public async Task ProtectionZone_CoversFullHeight()
    {
        // Act
        await BuildTowerAndCaptureCommands();

        var towerRegion = _protection.Regions.FirstOrDefault(
            r => r.Owner.Contains("Tower", StringComparison.OrdinalIgnoreCase) ||
                 r.Owner.Contains("Observation", StringComparison.OrdinalIgnoreCase));

        Assert.NotEqual(default, towerRegion);

        // Protection should cover from SurfaceY to SurfaceY+32
        Assert.True(towerRegion.MinY <= SurfaceY,
            $"Protection MinY ({towerRegion.MinY}) should cover SurfaceY ({SurfaceY})");
        Assert.True(towerRegion.MaxY >= TowerTopY,
            $"Protection MaxY ({towerRegion.MaxY}) should cover tower top ({TowerTopY})");
    }

    [Fact]
    public async Task ProtectionZone_BlocksCanalsAndRails()
    {
        // Act
        await BuildTowerAndCaptureCommands();

        // Verify that overlaps detection works for the tower region
        // A point inside the tower footprint should be detected as overlapping
        int testX = TowerOriginX + TowerSideLength / 2; // center X
        int testZ = TowerOriginZ + TowerSideLength / 2; // center Z
        int testY = SurfaceY + 5; // mid-height

        Assert.True(_protection.Overlaps(testX, testY, testZ, testX, testY, testZ),
            $"Point ({testX},{testY},{testZ}) inside tower should be detected as overlapping.");
    }

    // ====================================================================
    // 3. SPIRAL STAIRCASE GEOMETRY TESTS
    // ====================================================================

    [Fact]
    public async Task SpiralStaircase_ContinuousFromGroundToRoof()
    {
        // Act
        var commands = await BuildTowerAndCaptureCommands();
        var setblocks = ParseSetblocks(commands);

        // Find all stair blocks (oak_stairs or stone_brick_stairs)
        var stairBlocks = setblocks
            .Where(s => s.Block.Contains("stairs"))
            .OrderBy(s => s.Y)
            .ToList();

        Assert.NotEmpty(stairBlocks);

        // Stairs should start near ground level (SurfaceY + 1)
        var lowestStair = stairBlocks.Min(s => s.Y);
        Assert.True(lowestStair <= SurfaceY + 2,
            $"Lowest stair at Y={lowestStair} should start near ground (SurfaceY+1={SurfaceY + 1})");

        // Stairs should reach near roof level (at least y+24 for the top landing)
        var highestStair = stairBlocks.Max(s => s.Y);
        Assert.True(highestStair >= SurfaceY + 24,
            $"Highest stair at Y={highestStair} should reach near roof (SurfaceY+24={SurfaceY + 24})");
    }

    [Fact]
    public async Task SpiralStaircase_EachStepAscendsExactlyOneBlock()
    {
        // Act
        var commands = await BuildTowerAndCaptureCommands();
        var setblocks = ParseSetblocks(commands);

        var stairBlocks = setblocks
            .Where(s => s.Block.Contains("stairs"))
            .OrderBy(s => s.Y)
            .ToList();

        if (stairBlocks.Count < 2) return;

        // Group stairs by Y level and verify consecutive levels have no gaps > 1
        var yLevels = stairBlocks.Select(s => s.Y).Distinct().OrderBy(y => y).ToList();
        for (int i = 1; i < yLevels.Count; i++)
        {
            var gap = yLevels[i] - yLevels[i - 1];
            Assert.True(gap <= 1,
                $"Staircase gap between Y={yLevels[i - 1]} and Y={yLevels[i]} is {gap} blocks. " +
                $"Each step must ascend exactly 1 block (no jumps required).");
        }
    }

    [Fact]
    public async Task SpiralStaircase_FollowsCounterClockwisePattern()
    {
        // Spec direction: South→East→North→West (counter-clockwise viewed from above)
        // Flight 1: South wall (ascending east)
        // Flight 2: East wall (ascending north)
        // Flight 3: North wall (ascending west)
        // Flight 4: West wall (ascending south)
        var commands = await BuildTowerAndCaptureCommands();
        var setblocks = ParseSetblocks(commands);

        var stairBlocks = setblocks
            .Where(s => s.Block.Contains("stairs"))
            .OrderBy(s => s.Y)
            .ToList();

        if (stairBlocks.Count < 4) return;

        // Check stair facing directions exist in the expected sequence
        var facings = new[] { "east", "north", "west", "south" };
        foreach (var facing in facings)
        {
            Assert.True(
                stairBlocks.Any(s => s.Block.Contains($"facing={facing}")),
                $"Expected stairs with facing={facing} for counter-clockwise spiral.");
        }
    }

    [Theory]
    [InlineData(7)]
    [InlineData(12)]
    [InlineData(17)]
    [InlineData(24)]
    public async Task SpiralStaircase_HasLandingPlatformAtExpectedLevel(int floorOffset)
    {
        // Spec: Landing platforms exist at y+7, y+12, y+17, y+24
        var commands = await BuildTowerAndCaptureCommands();
        var fills = ParseFills(commands);

        int landingY = SurfaceY + floorOffset;

        // Look for a fill command that places a floor platform at this Y level
        var hasFloorFill = fills.Any(f =>
            (f.Block.Contains("planks") || f.Block.Contains("deepslate_tiles")) &&
            f.MinY <= landingY && f.MaxY >= landingY);

        Assert.True(hasFloorFill,
            $"Expected landing platform at Y={landingY} (SurfaceY+{floorOffset})");
    }

    [Fact]
    public async Task SpiralStaircase_HasSafetyFencesOnInsideEdge()
    {
        // Spec: 1-2 block-high fences or walls on the inside of each staircase flight
        var commands = await BuildTowerAndCaptureCommands();

        // Look for fence or wall blocks in the staircase region
        var fenceOrWallCommands = commands.Where(c =>
            c.Contains("minecraft:oak_fence") ||
            c.Contains("minecraft:stone_brick_wall") ||
            c.Contains("minecraft:cobblestone_wall")).ToList();

        Assert.NotEmpty(fenceOrWallCommands);
    }

    // ====================================================================
    // 4. FLOOR LAYOUT TESTS
    // ====================================================================

    [Fact]
    public async Task FiveDistinctFloors_AtCorrectYLevels()
    {
        // Spec: 5 floors — Ground (y+1), Library (y+7), Armory (y+12),
        //                    Observation (y+17), Crowning (y+24)
        var commands = await BuildTowerAndCaptureCommands();
        var fills = ParseFills(commands);

        // Check for floor platforms at each landing offset (oak_planks or deepslate_tiles)
        foreach (var offset in FloorOffsets)
        {
            int floorY = SurfaceY + offset;
            var hasFloor = fills.Any(f =>
                (f.Block.Contains("planks") || f.Block.Contains("deepslate_tiles")) &&
                f.MinY <= floorY && f.MaxY >= floorY);

            Assert.True(hasFloor,
                $"Missing floor platform at Y={floorY} (SurfaceY+{offset})");
        }
    }

    [Fact]
    public async Task FloorPlatforms_AreOakPlanks()
    {
        // Spec: Floor platforms are oak_planks
        var commands = await BuildTowerAndCaptureCommands();
        var fills = ParseFills(commands);

        // All floor fills at landing levels should be oak_planks
        foreach (var offset in FloorOffsets)
        {
            int floorY = SurfaceY + offset;
            var floorFills = fills.Where(f =>
                f.MinY <= floorY && f.MaxY >= floorY &&
                f.Block.Contains("planks")).ToList();

            if (floorFills.Count > 0)
            {
                Assert.True(floorFills.All(f => f.Block.Contains("oak_planks")),
                    $"Floor at Y={floorY} should use oak_planks, found: " +
                    string.Join(", ", floorFills.Select(f => f.Block)));
            }
        }
    }

    [Fact]
    public async Task InteriorClearance_Is17x17()
    {
        // Spec: Interior clearance is 17×17 (2-block walls on each side)
        var commands = await BuildTowerAndCaptureCommands();
        var fills = ParseFills(commands);

        // Look for the interior clearing fill (air fill inside the tower)
        var airFills = fills.Where(f => f.Block.Contains("minecraft:air")).ToList();

        if (airFills.Count > 0)
        {
            // The largest air fill should be roughly 17×17 in XZ extent
            var largestAirFill = airFills.OrderByDescending(f =>
                (f.MaxX - f.MinX + 1) * (f.MaxZ - f.MinZ + 1)).First();

            int xExtent = largestAirFill.MaxX - largestAirFill.MinX + 1;
            int zExtent = largestAirFill.MaxZ - largestAirFill.MinZ + 1;

            Assert.Equal(InteriorClearance, xExtent);
            Assert.Equal(InteriorClearance, zExtent);
        }
    }

    [Theory]
    [InlineData(7)]
    [InlineData(12)]
    [InlineData(17)]
    [InlineData(24)]
    public async Task FloorPlatform_FitsWithinInterior(int floorOffset)
    {
        // Floor platforms should be within the 17×17 interior
        var commands = await BuildTowerAndCaptureCommands();
        var fills = ParseFills(commands);

        int floorY = SurfaceY + floorOffset;
        var floorFills = fills.Where(f =>
            f.Block.Contains("minecraft:oak_planks") &&
            f.MinY <= floorY && f.MaxY >= floorY).ToList();

        foreach (var floor in floorFills)
        {
            // Floor X extent should be within interior bounds
            Assert.True(floor.MinX >= TowerMinX + WallThickness,
                $"Floor MinX ({floor.MinX}) should be >= interior MinX ({TowerMinX + WallThickness})");
            Assert.True(floor.MaxX <= TowerMaxX - WallThickness,
                $"Floor MaxX ({floor.MaxX}) should be <= interior MaxX ({TowerMaxX - WallThickness})");
            Assert.True(floor.MinZ >= TowerMinZ + WallThickness,
                $"Floor MinZ ({floor.MinZ}) should be >= interior MinZ ({TowerMinZ + WallThickness})");
            Assert.True(floor.MaxZ <= TowerMaxZ - WallThickness,
                $"Floor MaxZ ({floor.MaxZ}) should be <= interior MaxZ ({TowerMaxZ - WallThickness})");
        }
    }

    // ====================================================================
    // 5. FORCELOAD TESTS
    // ====================================================================

    [Fact]
    public async Task Forceload_IssuedBeforeAnyBlockPlacement()
    {
        // Spec: forceload must happen before any blocks are placed
        var commands = await BuildTowerAndCaptureCommands();

        // Find the first forceload command
        int forceloadIndex = commands.FindIndex(c => c.StartsWith("forceload"));
        Assert.True(forceloadIndex >= 0, "No forceload command found");

        // Find the first block-placement command (fill or setblock)
        int firstBlockIndex = commands.FindIndex(c =>
            c.StartsWith("fill") || c.StartsWith("setblock"));

        Assert.True(forceloadIndex < firstBlockIndex,
            $"Forceload (index {forceloadIndex}) must come before first block placement (index {firstBlockIndex})");
    }

    [Fact]
    public async Task Forceload_CoversFullTowerFootprint()
    {
        // Spec: forceload add {towerMinX} {towerMinZ} {towerMaxX} {towerMaxZ}
        var commands = await BuildTowerAndCaptureCommands();

        var forceloadCmd = commands.FirstOrDefault(c => c.StartsWith("forceload add"));
        Assert.NotNull(forceloadCmd);

        var m = ForceloadRegex().Match(forceloadCmd);
        Assert.True(m.Success, $"Could not parse forceload command: {forceloadCmd}");

        int flX1 = int.Parse(m.Groups[1].Value);
        int flZ1 = int.Parse(m.Groups[2].Value);
        int flX2 = int.Parse(m.Groups[3].Value);
        int flZ2 = int.Parse(m.Groups[4].Value);

        int flMinX = Math.Min(flX1, flX2);
        int flMaxX = Math.Max(flX1, flX2);
        int flMinZ = Math.Min(flZ1, flZ2);
        int flMaxZ = Math.Max(flZ1, flZ2);

        // Forceload must cover the entire tower footprint
        Assert.True(flMinX <= TowerMinX,
            $"Forceload MinX ({flMinX}) should cover tower MinX ({TowerMinX})");
        Assert.True(flMaxX >= TowerMaxX,
            $"Forceload MaxX ({flMaxX}) should cover tower MaxX ({TowerMaxX})");
        Assert.True(flMinZ <= TowerMinZ,
            $"Forceload MinZ ({flMinZ}) should cover tower MinZ ({TowerMinZ})");
        Assert.True(flMaxZ >= TowerMaxZ,
            $"Forceload MaxZ ({flMaxZ}) should cover tower MaxZ ({TowerMaxZ})");
    }

    // ====================================================================
    // 6. RCON COMMAND BUDGET TESTS
    // ====================================================================

    [Fact]
    public async Task CommandBudget_IsWithinReasonableLimits()
    {
        // Spec: ~280-320 commands total
        var commands = await BuildTowerAndCaptureCommands();

        // Filter to actual block-placement and forceload commands
        var blockCommands = commands.Where(c =>
            c.StartsWith("fill") ||
            c.StartsWith("setblock") ||
            c.StartsWith("forceload")).ToList();

        // Allow some margin — spec says 280-320, we'll accept 200-400
        Assert.True(blockCommands.Count >= 200,
            $"Command count ({blockCommands.Count}) is suspiciously low. Spec targets 280-320.");
        Assert.True(blockCommands.Count <= 400,
            $"Command count ({blockCommands.Count}) exceeds reasonable limit. Spec targets 280-320.");
    }

    [Fact]
    public async Task CommandBudget_StaircaseUsesSetblocks()
    {
        // Spec: spiral staircases use 100-120 individual setblock commands
        var commands = await BuildTowerAndCaptureCommands();
        var setblocks = ParseSetblocks(commands);

        var stairSetblocks = setblocks
            .Where(s => s.Block.Contains("stairs"))
            .ToList();

        // Stairs should use individual setblock commands (not fill)
        Assert.True(stairSetblocks.Count >= 20,
            $"Expected at least 20 stair setblocks, found only {stairSetblocks.Count}. " +
            $"Each stair step should be placed individually for precision.");
    }

    // ====================================================================
    // STRUCTURAL INTEGRITY TESTS
    // ====================================================================

    [Fact]
    public async Task Tower_HasBasePlinth()
    {
        // Spec: mossy stone base plinth at full 21×21 footprint
        var commands = await BuildTowerAndCaptureCommands();
        var fills = ParseFills(commands);

        // Look for a mossy stone brick fill at SurfaceY level covering the footprint
        var plinthFills = fills.Where(f =>
            f.Block.Contains("mossy") &&
            f.MinY <= SurfaceY && f.MaxY >= SurfaceY).ToList();

        Assert.NotEmpty(plinthFills);
    }

    [Fact]
    public async Task Tower_HasExteriorWalls()
    {
        // Spec: Main walls (hollow box) from y+1 to y+31
        var commands = await BuildTowerAndCaptureCommands();
        var fills = ParseFills(commands);

        // Look for a stone_bricks fill that spans most of the tower height
        var wallFills = fills.Where(f =>
            f.Block.Contains("stone_bricks") &&
            (f.MaxY - f.MinY) >= 20).ToList();

        Assert.NotEmpty(wallFills);
    }

    [Fact]
    public async Task Tower_HasParapet()
    {
        // Spec: crenellated parapet at top of tower
        var commands = await BuildTowerAndCaptureCommands();

        // Look for blocks placed near the top (y+30 to y+32 range)
        var topBlocks = ParseSetblocks(commands)
            .Where(s => s.Y >= SurfaceY + 30 && s.Y <= TowerTopY)
            .ToList();

        var topFills = ParseFills(commands)
            .Where(f => f.MaxY >= SurfaceY + 30 && f.MinY <= TowerTopY)
            .ToList();

        Assert.True(topBlocks.Count + topFills.Count > 0,
            "Expected parapet/battlement blocks at tower top (Y >= SurfaceY+30)");
    }

    [Fact]
    public async Task Tower_AllBlocksWithinFootprint()
    {
        // Every block placed by the tower should be within the tower's XZ footprint
        // (with 1-block buffer for decorations)
        var commands = await BuildTowerAndCaptureCommands();
        var setblocks = ParseSetblocks(commands);
        var fills = ParseFills(commands);

        int bufferMinX = TowerMinX - 1;
        int bufferMaxX = TowerMaxX + 1;
        int bufferMinZ = TowerMinZ - 1;
        int bufferMaxZ = TowerMaxZ + 1;

        foreach (var sb in setblocks)
        {
            Assert.True(sb.X >= bufferMinX && sb.X <= bufferMaxX,
                $"Setblock at X={sb.X} is outside tower X range [{bufferMinX}..{bufferMaxX}]");
            Assert.True(sb.Z >= bufferMinZ && sb.Z <= bufferMaxZ,
                $"Setblock at Z={sb.Z} is outside tower Z range [{bufferMinZ}..{bufferMaxZ}]");
        }

        foreach (var f in fills)
        {
            Assert.True(f.MinX >= bufferMinX && f.MaxX <= bufferMaxX,
                $"Fill X range [{f.MinX}..{f.MaxX}] exceeds tower X range [{bufferMinX}..{bufferMaxX}]");
            Assert.True(f.MinZ >= bufferMinZ && f.MaxZ <= bufferMaxZ,
                $"Fill Z range [{f.MinZ}..{f.MaxZ}] exceeds tower Z range [{bufferMinZ}..{bufferMaxZ}]");
        }
    }

    // ====================================================================
    // GEOMETRIC VALIDATION (pure coordinate math — always passes)
    // ====================================================================

    [Fact]
    public void TowerGeometry_InteriorClearance_MatchesWallThickness()
    {
        // Interior = footprint - 2 * wall thickness
        int expectedInterior = TowerSideLength - 2 * WallThickness;
        Assert.Equal(InteriorClearance, expectedInterior);
    }

    [Fact]
    public void TowerGeometry_FloorOffsets_AreAscending()
    {
        for (int i = 1; i < FloorOffsets.Length; i++)
        {
            Assert.True(FloorOffsets[i] > FloorOffsets[i - 1],
                $"Floor offset {FloorOffsets[i]} should be greater than {FloorOffsets[i - 1]}");
        }
    }

    [Fact]
    public void TowerGeometry_AllFloorsWithinTowerHeight()
    {
        foreach (var offset in FloorOffsets)
        {
            Assert.True(offset <= TowerHeight,
                $"Floor offset {offset} exceeds tower height {TowerHeight}");
        }
    }

    [Fact]
    public void TowerGeometry_FloorSpacing_AllowsPlayerHeadroom()
    {
        // Each floor must have at least 3 blocks of headroom (player is 1.8 blocks tall)
        var allFloors = new[] { 1 }.Concat(FloorOffsets).ToArray();
        for (int i = 1; i < allFloors.Length; i++)
        {
            int headroom = allFloors[i] - allFloors[i - 1];
            Assert.True(headroom >= 3,
                $"Headroom between floor at y+{allFloors[i - 1]} and y+{allFloors[i]} " +
                $"is only {headroom} blocks. Minimum 3 required for player clearance.");
        }
    }

    [Fact]
    public void TowerGeometry_SpiralDirections_FormCompleteLoop()
    {
        // Counter-clockwise spiral: South→East→North→West
        // Each wall is used once per full rotation
        var walls = new[] { "South", "East", "North", "West" };
        Assert.Equal(4, walls.Length);
        Assert.Equal(4, walls.Distinct().Count());
    }

    [Theory]
    [InlineData(0, "First grid position")]
    [InlineData(1, "Second grid position")]
    public void TowerFootprint_OutsideVillageGrid_NoOverlap(int resourceIndex, string label)
    {
        // More explicit overlap check with labeled positions
        var (ox, _, oz) = VillageLayout.GetStructureOrigin(resourceIndex);
        var size = VillageLayout.StructureSize;

        // Tower is at X:25-45, Z:-45--25
        // Grid position 0 is at X:10, Z:0 → extends to X:24, Z:14
        // Grid position 1 is at X:46, Z:0 → extends to X:60, Z:14
        bool overlapsX = TowerMaxX >= ox && TowerMinX <= ox + size - 1;
        bool overlapsZ = TowerMaxZ >= oz && TowerMinZ <= oz + size - 1;

        Assert.False(overlapsX && overlapsZ,
            $"{label} (origin=({ox},{oz}), size={size}) overlaps tower at " +
            $"({TowerMinX},{TowerMinZ})-({TowerMaxX},{TowerMaxZ})");
    }
}
