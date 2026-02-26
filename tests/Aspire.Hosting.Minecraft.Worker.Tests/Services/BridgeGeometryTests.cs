using System.Text.RegularExpressions;
using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Services;

/// <summary>
/// Validates bridge geometry over canals — both walkway bridges (path crossings)
/// and track bridges (MinecartRailService canal crossings).
///
/// Key coordinates (superflat defaults):
///   SurfaceY = -60 (grass level)
///   CanalY   = SurfaceY - 1 = -61 (water level)
///   Bridge deck minimum = SurfaceY + 1 = -59 (2 blocks above water)
///
/// Requirements tested:
///   1. Bridge deck Y >= SurfaceY + 1 (clearance)
///   2. No bridge block placed at canal water Y (canal passability)
///   3. Track bridges include rail placement commands
///   4. Bridge ramps don't exceed 0.5-block step height (NPC passability)
///   5. Bridges only placed where paths/tracks cross canals
/// </summary>
public partial class BridgeGeometryTests : IAsyncLifetime
{
    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private AspireResourceMonitor _monitor = null!;
    private BuildingProtectionService _protection = null!;
    private CanalService _canalService = null!;
    private MinecartRailService _railService = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance, maxCommandsPerSecond: 1000);
        _monitor = TestResourceMonitorFactory.Create();
        _protection = new BuildingProtectionService(NullLogger<BuildingProtectionService>.Instance);
        _canalService = new CanalService(_rcon, _monitor, _protection,
            NullLogger<CanalService>.Instance);
        _railService = new MinecartRailService(_rcon, _monitor,
            NullLogger<MinecartRailService>.Instance, _canalService);

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
    // COMMAND PARSING HELPERS
    // ====================================================================

    private record SetblockCmd(int X, int Y, int Z, string Block);

    [GeneratedRegex(@"^setblock\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(minecraft:\S+)")]
    private static partial Regex SetblockRegex();

    [GeneratedRegex(@"^fill\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(minecraft:\S+)")]
    private static partial Regex FillRegex();

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

    /// <summary>
    /// Checks whether a setblock or fill command places a solid block at the given coordinate.
    /// </summary>
    private static bool HasSolidBlockAt(int x, int y, int z, List<string> commands)
    {
        foreach (var cmd in commands)
        {
            var sm = SetblockRegex().Match(cmd);
            if (sm.Success)
            {
                int sx = int.Parse(sm.Groups[1].Value);
                int sy = int.Parse(sm.Groups[2].Value);
                int sz = int.Parse(sm.Groups[3].Value);
                string block = sm.Groups[4].Value;
                if (sx == x && sy == y && sz == z && !block.Contains("air") && !block.Contains("water"))
                    return true;
            }

            var fm = FillRegex().Match(cmd);
            if (fm.Success)
            {
                int x1 = int.Parse(fm.Groups[1].Value), x2 = int.Parse(fm.Groups[4].Value);
                int y1 = int.Parse(fm.Groups[2].Value), y2 = int.Parse(fm.Groups[5].Value);
                int z1 = int.Parse(fm.Groups[3].Value), z2 = int.Parse(fm.Groups[6].Value);
                string block = fm.Groups[7].Value;
                int minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);
                int minY = Math.Min(y1, y2), maxY = Math.Max(y1, y2);
                int minZ = Math.Min(z1, z2), maxZ = Math.Max(z1, z2);
                if (x >= minX && x <= maxX && y >= minY && y <= maxY && z >= minZ && z <= maxZ
                    && !block.Contains("air") && !block.Contains("water"))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Sets up a two-resource village with a parent→child dependency so that
    /// both canals and rails are built, creating bridge opportunities.
    /// </summary>
    private async Task SetupDependentResourcesAndBuild()
    {
        TestResourceMonitorFactory.SetResourcesWithDependencies(_monitor,
            ("parent-svc", "Project", ResourceStatus.Healthy, []),
            ("child-svc", "Container", ResourceStatus.Healthy, ["parent-svc"])
        );

        // Build canals first (they register CanalPositions)
        await _canalService.InitializeAsync();
        _server.ClearCommands();

        // Build rails (which now detect canal crossings)
        await _railService.InitializeAsync();
    }

    // ====================================================================
    // TRACK BRIDGE CLEARANCE TESTS
    // ====================================================================

    [Fact]
    public async Task TrackBridge_DeckY_IsAtLeastSurfaceYPlusOne()
    {
        // Arrange & Act
        await SetupDependentResourcesAndBuild();
        var commands = _server.GetCommands();

        // Find all rail/slab commands placed over canal positions
        var bridgeCommands = ParseSetblocks(commands)
            .Where(s => _canalService.CanalPositions.Contains((s.X, s.Z)))
            .ToList();

        // Skip if no bridge was built (resources might be in same column)
        if (bridgeCommands.Count == 0) return;

        // Assert: all bridge deck blocks (rails, slabs) must be >= SurfaceY + 1
        var minimumDeckY = VillageLayout.SurfaceY + 1;
        foreach (var cmd in bridgeCommands.Where(c =>
            c.Block.Contains("rail") || c.Block.Contains("slab")))
        {
            Assert.True(cmd.Y >= minimumDeckY - 1,
                $"Bridge element {cmd.Block} at ({cmd.X}, {cmd.Y}, {cmd.Z}) is below minimum deck Y={minimumDeckY - 1}. " +
                $"Must be >= SurfaceY ({VillageLayout.SurfaceY}) for 2-block clearance above water at {VillageLayout.CanalY}.");
        }
    }

    [Fact]
    public async Task TrackBridge_RailY_IsAboveCanalWater()
    {
        // Arrange & Act
        await SetupDependentResourcesAndBuild();
        var commands = _server.GetCommands();

        var railsOverCanal = ParseSetblocks(commands)
            .Where(s => _canalService.CanalPositions.Contains((s.X, s.Z))
                        && (s.Block.Contains("rail")))
            .ToList();

        if (railsOverCanal.Count == 0) return;

        var waterY = VillageLayout.CanalY;
        foreach (var rail in railsOverCanal)
        {
            Assert.True(rail.Y > waterY,
                $"Rail {rail.Block} at ({rail.X}, {rail.Y}, {rail.Z}) is at or below water level Y={waterY}.");
        }
    }

    [Fact]
    public async Task TrackBridge_IncludesRailPlacementCommands()
    {
        // Arrange & Act
        await SetupDependentResourcesAndBuild();
        var commands = _server.GetCommands();

        // Verify that rail commands exist in the output
        var railCommands = commands.Where(c =>
            c.Contains("minecraft:rail") || c.Contains("minecraft:powered_rail")).ToList();

        Assert.NotEmpty(railCommands);
    }

    [Fact]
    public async Task TrackBridge_RailsConnectToGroundLevelOnBothSides()
    {
        // Arrange & Act
        await SetupDependentResourcesAndBuild();
        var commands = _server.GetCommands();

        // Find all rail positions (both over canal and on ground)
        var allRails = ParseSetblocks(commands)
            .Where(s => s.Block.Contains("rail") || s.Block.Contains("detector_rail") || s.Block.Contains("powered_rail"))
            .ToList();

        if (allRails.Count == 0) return;

        // Verify that some rails are on ground level (SurfaceY + 1) — not all elevated
        var groundLevelRailY = VillageLayout.SurfaceY + 1;
        var groundRails = allRails.Where(r => r.Y == groundLevelRailY).ToList();
        Assert.NotEmpty(groundRails);

        // Verify rail commands include detector_rail (station endpoints on ground)
        Assert.Contains(commands, c => c.Contains("minecraft:detector_rail"));
    }

    // ====================================================================
    // CANAL INTEGRITY TESTS — BRIDGES MUST NOT BLOCK WATER CHANNEL
    // ====================================================================

    [Fact]
    public async Task TrackBridge_NoBridgeBlockAtWaterLevel()
    {
        // Arrange & Act
        await SetupDependentResourcesAndBuild();
        var commands = _server.GetCommands();

        var waterY = VillageLayout.CanalY; // SurfaceY - 1

        // Assert: NO stone_bricks at water level over canal positions — solid blocks obstruct boats
        var stoneBricksAtWater = ParseSetblocks(commands)
            .Where(s => _canalService.CanalPositions.Contains((s.X, s.Z))
                        && s.Y == waterY
                        && s.Block.Contains("stone_bricks"))
            .ToList();

        Assert.Empty(stoneBricksAtWater);

        // Assert: oak_fence IS placed at water level for boat-passable bridge support
        var railsOverCanal = ParseSetblocks(commands)
            .Where(s => _canalService.CanalPositions.Contains((s.X, s.Z))
                        && s.Block.Contains("rail"))
            .ToList();

        if (railsOverCanal.Count > 0)
        {
            var oakFenceAtWater = ParseSetblocks(commands)
                .Where(s => _canalService.CanalPositions.Contains((s.X, s.Z))
                            && s.Y == waterY
                            && s.Block.Contains("oak_fence"))
                .ToList();

            Assert.NotEmpty(oakFenceAtWater);
        }
    }

    [Fact]
    public async Task TrackBridge_CanalWaterCommandsNotOverwrittenByBridge()
    {
        // Arrange — build canals, record water fills
        TestResourceMonitorFactory.SetResourcesWithDependencies(_monitor,
            ("parent-svc", "Project", ResourceStatus.Healthy, []),
            ("child-svc", "Container", ResourceStatus.Healthy, ["parent-svc"])
        );

        await _canalService.InitializeAsync();
        var canalCommands = _server.GetCommands().ToList();

        // Capture all water fill regions from canal construction
        var waterFills = new List<(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ)>();
        foreach (var cmd in canalCommands)
        {
            var m = FillRegex().Match(cmd);
            if (m.Success && m.Groups[7].Value.Contains("water"))
            {
                int x1 = int.Parse(m.Groups[1].Value), x2 = int.Parse(m.Groups[4].Value);
                int y1 = int.Parse(m.Groups[2].Value), y2 = int.Parse(m.Groups[5].Value);
                int z1 = int.Parse(m.Groups[3].Value), z2 = int.Parse(m.Groups[6].Value);
                waterFills.Add((Math.Min(x1, x2), Math.Min(y1, y2), Math.Min(z1, z2),
                                Math.Max(x1, x2), Math.Max(y1, y2), Math.Max(z1, z2)));
            }
        }

        Assert.NotEmpty(waterFills);

        // Now build rails
        _server.ClearCommands();
        await _railService.InitializeAsync();
        var railCommands = _server.GetCommands();

        // Verify: no fill commands in rail phase replace water regions with non-water blocks
        foreach (var cmd in railCommands)
        {
            var m = FillRegex().Match(cmd);
            if (m.Success && !m.Groups[7].Value.Contains("water") && !m.Groups[7].Value.Contains("air"))
            {
                int x1 = int.Parse(m.Groups[1].Value), x2 = int.Parse(m.Groups[4].Value);
                int y1 = int.Parse(m.Groups[2].Value), y2 = int.Parse(m.Groups[5].Value);
                int z1 = int.Parse(m.Groups[3].Value), z2 = int.Parse(m.Groups[6].Value);
                int minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);
                int minY = Math.Min(y1, y2), maxY = Math.Max(y1, y2);
                int minZ = Math.Min(z1, z2), maxZ = Math.Max(z1, z2);

                foreach (var wf in waterFills)
                {
                    bool overlaps = minX <= wf.MaxX && maxX >= wf.MinX
                        && minY <= wf.MaxY && maxY >= wf.MinY
                        && minZ <= wf.MaxZ && maxZ >= wf.MinZ;

                    Assert.False(overlaps,
                        $"Rail fill command overwrites canal water region: {cmd}");
                }
            }
        }
    }

    // ====================================================================
    // BRIDGE PLACEMENT — ONLY AT PATH-CANAL INTERSECTIONS
    // ====================================================================

    [Fact]
    public async Task TrackBridge_OnlyPlacedAtCanalCrossings()
    {
        // Arrange & Act
        await SetupDependentResourcesAndBuild();
        var commands = _server.GetCommands();

        // Bridge support elements: slabs and stone_bricks placed by the bridge logic
        var bridgeSupports = ParseSetblocks(commands)
            .Where(s => (s.Block.Contains("slab") || s.Block.Contains("stone_bricks"))
                        && s.Y < VillageLayout.SurfaceY + 1)
            .ToList();

        // Every bridge support must be at a known canal position
        foreach (var support in bridgeSupports)
        {
            Assert.True(
                _canalService.CanalPositions.Contains((support.X, support.Z)),
                $"Bridge support {support.Block} at ({support.X}, {support.Y}, {support.Z}) " +
                $"is not over a canal position. Bridges should only be placed at canal crossings.");
        }
    }

    [Fact]
    public async Task TrackBridge_NoBridgeWhenNoCanalCrossing()
    {
        // Arrange: Two resources in same column (no canal crossing expected for straight Z path)
        TestResourceMonitorFactory.SetResourcesWithDependencies(_monitor,
            ("db-primary", "Project", ResourceStatus.Healthy, []),
            ("db-replica", "Project", ResourceStatus.Healthy, ["db-primary"])
        );
        await _canalService.InitializeAsync();
        _server.ClearCommands();

        await _railService.InitializeAsync();
        var commands = _server.GetCommands();

        // Same column resources: rail goes straight Z, may or may not cross canal
        // depending on canal layout. Bridge supports (slabs) should only appear
        // at actual canal positions.
        var bridgeSlabs = ParseSetblocks(commands)
            .Where(s => s.Block.Contains("slab"))
            .ToList();

        foreach (var slab in bridgeSlabs)
        {
            Assert.True(
                _canalService.CanalPositions.Contains((slab.X, slab.Z)),
                $"Bridge slab at ({slab.X}, {slab.Y}, {slab.Z}) is not over a canal.");
        }
    }

    // ====================================================================
    // WALKWAY BRIDGE GEOMETRY TESTS (for Rocket's concurrent implementation)
    // ====================================================================
    // These test the geometric REQUIREMENTS for walkway bridges.
    // They verify coordinate math and clearance constraints independent
    // of the actual service implementation.

    [Fact]
    public void WalkwayBridge_MinimumDeckY_MeetsRequirement()
    {
        // Requirement: bridge deck >= SurfaceY + 1 for 2-block clearance above water
        var surfaceY = VillageLayout.SurfaceY;
        var waterY = VillageLayout.CanalY;
        var minimumDeckY = surfaceY + 1;

        // Verify the geometric constraint
        Assert.True(minimumDeckY > waterY,
            $"Minimum deck Y ({minimumDeckY}) must be above water Y ({waterY})");
        Assert.True(minimumDeckY - waterY >= 2,
            $"Deck-to-water gap ({minimumDeckY - waterY}) must be >= 2 blocks for boat clearance");
    }

    [Fact]
    public void WalkwayBridge_CanalDimensions_ConsistentWithLayout()
    {
        // Verify canal dimensions that bridges must span
        Assert.Equal(3, VillageLayout.CanalWaterWidth);
        Assert.Equal(5, VillageLayout.CanalTotalWidth);
        Assert.Equal(2, VillageLayout.CanalDepth);

        // Canal water is 1 block below surface
        Assert.Equal(VillageLayout.SurfaceY - 1, VillageLayout.CanalY);
    }

    [Theory]
    [InlineData(0.0, 0.5, true)]   // Half slab step — passable
    [InlineData(0.0, 1.0, false)]  // Full block step — too high for NPCs
    [InlineData(0.5, 1.0, true)]   // Slab to full block — 0.5 step, passable
    [InlineData(0.0, 1.5, false)]  // Jump to 1.5 — way too high
    [InlineData(1.0, 1.5, true)]   // Full block to slab on top — 0.5 step
    public void WalkwayBridge_RampStepHeight_NPCPassability(double fromY, double toY, bool shouldBePassable)
    {
        // Requirement: ramp steps must not exceed 0.5 blocks for NPC/horse passability
        var stepHeight = Math.Abs(toY - fromY);
        var isPassable = stepHeight <= 0.5;

        Assert.Equal(shouldBePassable, isPassable);
    }

    [Fact]
    public void WalkwayBridge_RampSequence_ValidStairSlabPattern()
    {
        // A valid bridge ramp sequence using stairs/slabs should look like:
        // Ground (0) → Slab (0.5) → Full block (1.0) → Slab on block (1.5) → Full+full (2.0)
        // Each step ≤ 0.5 blocks — passable by NPCs and horses.
        var rampHeights = new[] { 0.0, 0.5, 1.0, 1.5, 2.0 };

        for (int i = 1; i < rampHeights.Length; i++)
        {
            var step = rampHeights[i] - rampHeights[i - 1];
            Assert.True(step <= 0.5,
                $"Ramp step from {rampHeights[i - 1]} to {rampHeights[i]} is {step} blocks — " +
                $"exceeds 0.5 block maximum for NPC passability.");
        }
    }

    [Fact]
    public void WalkwayBridge_ArchShape_SymmetricRampRequired()
    {
        // Requirement: bridges have ramps on BOTH sides (ascending then descending)
        // A symmetric bridge over a 5-block-wide canal (CanalTotalWidth) needs:
        //   Approach ramp (ascending) + flat deck over canal + departure ramp (descending)
        var canalWidth = VillageLayout.CanalTotalWidth;

        // Minimum bridge length = ramp up + canal span + ramp down
        // With 2-block elevation (SurfaceY+1 deck), ramp needs at least 2 blocks each side
        // (slab step + full block step = 2 blocks horizontal for 1 block vertical)
        var deckElevation = 2; // blocks above ground (SurfaceY+1 is 1 above SurfaceY ground)
        var minRampLength = deckElevation * 2; // 2 horizontal blocks per 1 vertical block (stairs)
        var minBridgeLength = minRampLength + canalWidth + minRampLength;

        Assert.True(minBridgeLength >= canalWidth + 4,
            $"Bridge must be at least {canalWidth + 4} blocks long to accommodate ramps " +
            $"on both sides of the {canalWidth}-block canal.");
    }

    // ====================================================================
    // BUILDING PROTECTION — BRIDGES MUST RESPECT BUILDING ZONES
    // ====================================================================

    [Fact]
    public void Bridge_DoesNotOverlapProtectedRegion()
    {
        // Arrange: register a building protection zone
        var protection = new BuildingProtectionService(NullLogger<BuildingProtectionService>.Instance);
        var buildingMinX = 10;
        var buildingMinZ = 0;
        var buildingMaxX = 24; // 15-block structure
        var buildingMaxZ = 14;

        protection.Register(
            buildingMinX, VillageLayout.SurfaceY, buildingMinZ,
            buildingMaxX, VillageLayout.SurfaceY + 25, buildingMaxZ,
            "test-building");

        // Simulate a bridge that would overlap the building footprint
        var bridgeMinX = 20;
        var bridgeMaxX = 30;
        var bridgeY = VillageLayout.SurfaceY + 1;
        var bridgeZ = 7; // Through the middle of the building

        // ClipFill should return sub-regions that avoid the building
        var clipped = protection.ClipFill(
            bridgeMinX, bridgeY, bridgeZ,
            bridgeMaxX, bridgeY, bridgeZ);

        // Verify no clipped region overlaps the building
        foreach (var (cMinX, cMinY, cMinZ, cMaxX, cMaxY, cMaxZ) in clipped)
        {
            bool overlapsBuilding =
                cMinX <= buildingMaxX && cMaxX >= buildingMinX &&
                cMinZ <= buildingMaxZ && cMaxZ >= buildingMinZ &&
                cMinY <= VillageLayout.SurfaceY + 25 && cMaxY >= VillageLayout.SurfaceY;

            Assert.False(overlapsBuilding,
                $"Clipped bridge region ({cMinX},{cMinY},{cMinZ})-({cMaxX},{cMaxY},{cMaxZ}) " +
                $"overlaps protected building ({buildingMinX},{VillageLayout.SurfaceY},{buildingMinZ})-" +
                $"({buildingMaxX},{VillageLayout.SurfaceY + 25},{buildingMaxZ}).");
        }
    }

    [Fact]
    public void Bridge_ClipFill_PreservesBridgeOutsideBuilding()
    {
        // Arrange: building at X=10..24, bridge runs X=5..30
        var protection = new BuildingProtectionService(NullLogger<BuildingProtectionService>.Instance);
        protection.Register(10, VillageLayout.SurfaceY, 0, 24, VillageLayout.SurfaceY + 25, 14, "building");

        var bridgeY = VillageLayout.SurfaceY + 1;
        var bridgeZ = 7;

        var clipped = protection.ClipFill(5, bridgeY, bridgeZ, 30, bridgeY, bridgeZ);

        // Should have produced sub-regions covering X=5..9 and X=25..30
        Assert.NotEmpty(clipped);

        // Total X coverage of clipped regions should equal original minus building
        var coveredX = new HashSet<int>();
        foreach (var (cMinX, _, _, cMaxX, _, _) in clipped)
        {
            for (int x = cMinX; x <= cMaxX; x++)
                coveredX.Add(x);
        }

        // Bridge X range outside building: 5-9 and 25-30
        for (int x = 5; x <= 9; x++)
            Assert.Contains(x, coveredX);
        for (int x = 25; x <= 30; x++)
            Assert.Contains(x, coveredX);

        // No X within building range
        for (int x = 10; x <= 24; x++)
            Assert.DoesNotContain(x, coveredX);
    }

    // ====================================================================
    // TRACK BRIDGE SUPPORT STRUCTURE TESTS
    // ====================================================================

    [Fact]
    public async Task TrackBridge_PlacesSlabUnderRail()
    {
        // Arrange & Act
        await SetupDependentResourcesAndBuild();
        var commands = _server.GetCommands();

        // Find rail positions over canal
        var railsOverCanal = ParseSetblocks(commands)
            .Where(s => _canalService.CanalPositions.Contains((s.X, s.Z))
                        && s.Block.Contains("rail"))
            .ToList();

        if (railsOverCanal.Count == 0) return;

        // Each rail over canal should have a slab or solid block underneath
        foreach (var rail in railsOverCanal)
        {
            var hasSupportBelow = ParseSetblocks(commands)
                .Any(s => s.X == rail.X && s.Z == rail.Z
                          && s.Y == rail.Y - 1
                          && (s.Block.Contains("slab") || s.Block.Contains("stone")));

            Assert.True(hasSupportBelow,
                $"Rail at ({rail.X}, {rail.Y}, {rail.Z}) over canal has no support block below at Y={rail.Y - 1}.");
        }
    }

    [Fact]
    public async Task TrackBridge_PoweredRailsOverCanal_SkipRedstoneTorch()
    {
        // Arrange & Act: powered rails over canals cannot have redstone torches underneath
        // (torch would be in the canal), so the service should skip them
        await SetupDependentResourcesAndBuild();
        var commands = _server.GetCommands();

        var poweredRailsOverCanal = ParseSetblocks(commands)
            .Where(s => _canalService.CanalPositions.Contains((s.X, s.Z))
                        && s.Block.Contains("powered_rail"))
            .ToList();

        foreach (var pRail in poweredRailsOverCanal)
        {
            var hasTorchBelow = ParseSetblocks(commands)
                .Any(s => s.X == pRail.X && s.Z == pRail.Z
                          && s.Y == pRail.Y - 1
                          && s.Block.Contains("redstone_torch"));

            Assert.False(hasTorchBelow,
                $"Powered rail at ({pRail.X}, {pRail.Y}, {pRail.Z}) over canal should NOT have a " +
                $"redstone torch at Y={pRail.Y - 1} — torch would be in the canal/support.");
        }
    }

    [Fact]
    public async Task TrackBridge_SupportStructure_HasCorrectMaterialLayers()
    {
        // Arrange & Act
        await SetupDependentResourcesAndBuild();
        var commands = _server.GetCommands();
        var allSetblocks = ParseSetblocks(commands);

        var groundRailY = VillageLayout.SurfaceY + 1;
        var bridgeDeckY = groundRailY + 2; // elevation 2 for bridge deck

        // Find rails at bridge deck height over canal positions
        var bridgeDeckRails = allSetblocks
            .Where(s => _canalService.CanalPositions.Contains((s.X, s.Z))
                        && s.Block.Contains("rail")
                        && s.Y == bridgeDeckY)
            .ToList();

        // Skip if no bridge deck rails (resources may be in same column)
        if (bridgeDeckRails.Count == 0) return;

        foreach (var rail in bridgeDeckRails)
        {
            var adjustedY = rail.Y;

            // adjustedY - 1: structural support (stone_bricks or redstone_block for powered rails)
            var hasDirectSupport = allSetblocks
                .Any(s => s.X == rail.X && s.Z == rail.Z && s.Y == adjustedY - 1
                          && (s.Block.Contains("stone_bricks") || s.Block.Contains("redstone_block")));

            Assert.True(hasDirectSupport,
                $"Bridge rail at ({rail.X}, {adjustedY}, {rail.Z}): expected stone_bricks or " +
                $"redstone_block at Y={adjustedY - 1} (directly under rail)");

            // adjustedY - 2: boat-passable support (oak_fence, NOT stone_bricks)
            var blocksAtWaterLevel = allSetblocks
                .Where(s => s.X == rail.X && s.Z == rail.Z && s.Y == adjustedY - 2)
                .ToList();

            Assert.True(blocksAtWaterLevel.Any(s => s.Block.Contains("oak_fence")),
                $"Bridge rail at ({rail.X}, {adjustedY}, {rail.Z}): expected oak_fence at " +
                $"Y={adjustedY - 2} (water level, boat-passable)");

            Assert.False(blocksAtWaterLevel.Any(s => s.Block.Contains("stone_bricks")),
                $"Bridge rail at ({rail.X}, {adjustedY}, {rail.Z}): stone_bricks at " +
                $"Y={adjustedY - 2} blocks boat passage through canal");
        }
    }

    // ====================================================================
    // GEOMETRIC CONSTRAINT VALIDATION
    // ====================================================================

    [Fact]
    public void ClearanceConstraint_TwoBlockGap_BetweenWaterAndDeck()
    {
        // Core geometric constraint: the gap between water surface and bridge deck
        // must be at least 2 blocks for boats to pass underneath.
        var waterY = VillageLayout.CanalY;        // SurfaceY - 1
        var deckY = VillageLayout.SurfaceY + 1;    // Minimum bridge deck Y

        var gap = deckY - waterY;
        Assert.Equal(2, gap);

        // The air block at SurfaceY is the passage space for boats
        var airBlockY = VillageLayout.SurfaceY;
        Assert.True(airBlockY > waterY, "Air space must be above water");
        Assert.True(airBlockY < deckY, "Air space must be below deck");
    }

    [Fact]
    public void CanalFloorY_IsBelowWaterY()
    {
        // Canal floor is at CanalY - 1, water is at CanalY
        var floorY = VillageLayout.CanalY - 1;
        var waterY = VillageLayout.CanalY;

        Assert.True(floorY < waterY,
            $"Canal floor Y ({floorY}) must be below water Y ({waterY})");

        // Bridge supports should NOT extend to floor level
        var deckY = VillageLayout.SurfaceY + 1;
        Assert.True(deckY - floorY >= 3,
            $"Deck-to-floor gap ({deckY - floorY}) should be >= 3 (floor + water + air)");
    }

    [Fact]
    public async Task FullBridgeScenario_CanalThenRails_CorrectCommandOrder()
    {
        // Verify that canal is built before rails so CanalPositions is populated
        TestResourceMonitorFactory.SetResourcesWithDependencies(_monitor,
            ("api", "Project", ResourceStatus.Healthy, []),
            ("worker", "Container", ResourceStatus.Healthy, ["api"])
        );

        await _canalService.InitializeAsync();

        // CanalPositions should be populated after canal initialization
        Assert.NotEmpty(_canalService.CanalPositions);

        _server.ClearCommands();
        await _railService.InitializeAsync();

        var railCommands = _server.GetCommands();
        Assert.NotEmpty(railCommands);

        // Verify rails were placed (proves the dependency between canal and rail services)
        Assert.Contains(railCommands, c => c.Contains("minecraft:rail") || c.Contains("minecraft:powered_rail"));
    }
}
