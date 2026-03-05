using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Tests for bridge/fruit stand collision detection logic.
/// When the BridgeService places walkway bridges across canals, bridges that overlap
/// the fruit stand (VillagerService market stall at village center) must be shifted
/// east or west to avoid destroying it. The shift direction depends on the bridge's
/// relative position to the fruit stand.
///
/// Proactive tests — written from requirements while Rocket implements collision avoidance.
/// These tests use the VillageLayout geometry directly to validate collision detection
/// and shift logic independent of RCON commands.
/// </summary>
public class BridgeCollisionDetectionTests
{
    // ====================================================================
    // GEOMETRY HELPERS (mirror expected collision detection contract)
    // ====================================================================

    /// <summary>
    /// Represents a bridge placement with center X and center Z (canal crossing point).
    /// </summary>
    private record BridgePlacement(int CenterX, int CenterZ, int Width = 5);

    /// <summary>
    /// Represents the fruit stand bounding box (5 wide × 3 deep, from VillagerService).
    /// </summary>
    private record FruitStandBounds(int MinX, int MinZ, int MaxX, int MaxZ);

    /// <summary>
    /// Gets the fruit stand bounds for a given resource count, using the same logic as VillagerService.
    /// Stand is 5×3 blocks, centered on the village midpoint.
    /// </summary>
    private static FruitStandBounds GetFruitStandBounds(int resourceCount)
    {
        var (fMinX, fMinZ, fMaxX, fMaxZ) = VillageLayout.GetVillageBounds(resourceCount);
        var standX = (fMinX + fMaxX) / 2 - 2; // Same offset as VillagerService
        var standZ = (fMinZ + fMaxZ) / 2;
        // Stand is 5 wide (X to X+4) and 3 deep (Z to Z+2), plus 1-block buffer
        return new FruitStandBounds(standX - 1, standZ - 1, standX + 5, standZ + 3);
    }

    /// <summary>
    /// Checks if a bridge placement overlaps the fruit stand bounds.
    /// Bridge occupies CenterX ± Width/2, CenterZ ± canal wall extent.
    /// </summary>
    private static bool BridgeOverlapsFruitStand(BridgePlacement bridge, FruitStandBounds stand)
    {
        var halfW = bridge.Width / 2;
        var bridgeWestX = bridge.CenterX - halfW;
        var bridgeEastX = bridge.CenterX + halfW;
        // Bridge spans ~3 blocks south + canal + 3 blocks north of canal center
        var bridgeZMin = bridge.CenterZ - VillageLayout.CanalWaterWidth / 2 - 4;
        var bridgeZMax = bridge.CenterZ + VillageLayout.CanalWaterWidth / 2 + 4;

        return bridgeEastX >= stand.MinX && bridgeWestX <= stand.MaxX &&
               bridgeZMax >= stand.MinZ && bridgeZMin <= stand.MaxZ;
    }

    /// <summary>
    /// Shifts a bridge placement away from the fruit stand.
    /// If bridge is west of stand center, shift further west; if east, shift further east.
    /// </summary>
    private static BridgePlacement ShiftBridgeAwayFromStand(BridgePlacement bridge, FruitStandBounds stand)
    {
        var standCenterX = (stand.MinX + stand.MaxX) / 2;
        var shiftAmount = bridge.Width + 2; // Full bridge width + buffer

        if (bridge.CenterX <= standCenterX)
        {
            // Bridge is to the west — shift further west
            return bridge with { CenterX = stand.MinX - bridge.Width / 2 - 2 };
        }
        else
        {
            // Bridge is to the east — shift further east
            return bridge with { CenterX = stand.MaxX + bridge.Width / 2 + 2 };
        }
    }

    // ====================================================================
    // OVERLAP DETECTION TESTS
    // ====================================================================

    [Fact]
    public void Bridge_OverlappingFruitStand_IsDetected()
    {
        var stand = GetFruitStandBounds(10);
        // Place bridge directly at the stand center X
        var bridge = new BridgePlacement(
            (stand.MinX + stand.MaxX) / 2,
            (stand.MinZ + stand.MaxZ) / 2);

        Assert.True(BridgeOverlapsFruitStand(bridge, stand));
    }

    [Fact]
    public void Bridge_FarFromFruitStand_DoesNotOverlap()
    {
        var stand = GetFruitStandBounds(10);
        // Place bridge 50 blocks east of the stand
        var bridge = new BridgePlacement(stand.MaxX + 50, stand.MinZ);

        Assert.False(BridgeOverlapsFruitStand(bridge, stand));
    }

    [Fact]
    public void Bridge_JustOutsideWestEdge_DoesNotOverlap()
    {
        var stand = GetFruitStandBounds(10);
        // Bridge edge exactly one block west of stand
        var bridge = new BridgePlacement(stand.MinX - 5, stand.MinZ - 20);

        Assert.False(BridgeOverlapsFruitStand(bridge, stand));
    }

    [Fact]
    public void Bridge_JustTouchingEastEdge_IsDetected()
    {
        var stand = GetFruitStandBounds(10);
        // Bridge western edge touches stand eastern edge
        var bridge = new BridgePlacement(
            stand.MaxX,
            (stand.MinZ + stand.MaxZ) / 2);

        Assert.True(BridgeOverlapsFruitStand(bridge, stand));
    }

    // ====================================================================
    // SHIFT DIRECTION TESTS
    // ====================================================================

    [Fact]
    public void ShiftedBridge_WestOfStand_ShiftsWest()
    {
        var stand = GetFruitStandBounds(10);
        var standCenterX = (stand.MinX + stand.MaxX) / 2;
        var bridge = new BridgePlacement(standCenterX - 1, (stand.MinZ + stand.MaxZ) / 2);

        var shifted = ShiftBridgeAwayFromStand(bridge, stand);

        Assert.True(shifted.CenterX < stand.MinX,
            $"Shifted bridge (X={shifted.CenterX}) should be west of stand MinX ({stand.MinX})");
    }

    [Fact]
    public void ShiftedBridge_EastOfStand_ShiftsEast()
    {
        var stand = GetFruitStandBounds(10);
        var standCenterX = (stand.MinX + stand.MaxX) / 2;
        var bridge = new BridgePlacement(standCenterX + 1, (stand.MinZ + stand.MaxZ) / 2);

        var shifted = ShiftBridgeAwayFromStand(bridge, stand);

        Assert.True(shifted.CenterX > stand.MaxX,
            $"Shifted bridge (X={shifted.CenterX}) should be east of stand MaxX ({stand.MaxX})");
    }

    [Fact]
    public void ShiftedBridge_NoLongerOverlapsFruitStand()
    {
        var stand = GetFruitStandBounds(10);
        var bridge = new BridgePlacement(
            (stand.MinX + stand.MaxX) / 2,
            (stand.MinZ + stand.MaxZ) / 2);

        Assert.True(BridgeOverlapsFruitStand(bridge, stand), "Precondition: bridge should overlap initially");

        var shifted = ShiftBridgeAwayFromStand(bridge, stand);

        Assert.False(BridgeOverlapsFruitStand(shifted, stand),
            "After shifting, bridge should no longer overlap fruit stand");
    }

    // ====================================================================
    // NON-OVERLAPPING BRIDGE STAYS PUT
    // ====================================================================

    [Fact]
    public void Bridge_NotOverlapping_StaysAtOriginalPosition()
    {
        var stand = GetFruitStandBounds(10);
        // Bridge far from stand
        var bridge = new BridgePlacement(stand.MaxX + 30, stand.MaxZ + 30);

        // No overlap means no shift needed
        var overlaps = BridgeOverlapsFruitStand(bridge, stand);
        Assert.False(overlaps);

        // If collision logic only shifts when overlapping, position stays
        var result = overlaps ? ShiftBridgeAwayFromStand(bridge, stand) : bridge;
        Assert.Equal(bridge.CenterX, result.CenterX);
        Assert.Equal(bridge.CenterZ, result.CenterZ);
    }

    // ====================================================================
    // COORDINATE VALIDITY TESTS
    // ====================================================================

    [Fact]
    public void ShiftedBridge_HasPositiveWidth()
    {
        var stand = GetFruitStandBounds(10);
        var bridge = new BridgePlacement(
            (stand.MinX + stand.MaxX) / 2,
            (stand.MinZ + stand.MaxZ) / 2);

        var shifted = ShiftBridgeAwayFromStand(bridge, stand);

        Assert.Equal(bridge.Width, shifted.Width);
        Assert.True(shifted.Width > 0);
    }

    [Fact]
    public void ShiftedBridge_StaysWithinVillageFenceBounds()
    {
        var resourceCount = 10;
        var stand = GetFruitStandBounds(resourceCount);
        var (fMinX, fMinZ, fMaxX, fMaxZ) = VillageLayout.GetFencePerimeter(resourceCount);

        var bridge = new BridgePlacement(
            (stand.MinX + stand.MaxX) / 2,
            (stand.MinZ + stand.MaxZ) / 2);

        var shifted = ShiftBridgeAwayFromStand(bridge, stand);
        var halfW = shifted.Width / 2;

        Assert.True(shifted.CenterX - halfW > fMinX,
            $"Bridge west edge ({shifted.CenterX - halfW}) should be inside fence MinX ({fMinX})");
        Assert.True(shifted.CenterX + halfW < fMaxX,
            $"Bridge east edge ({shifted.CenterX + halfW}) should be inside fence MaxX ({fMaxX})");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(20)]
    public void FruitStandBounds_AreConsistentForVariousResourceCounts(int resourceCount)
    {
        var stand = GetFruitStandBounds(resourceCount);

        // Stand should have positive dimensions
        Assert.True(stand.MaxX > stand.MinX, "Stand should have positive X extent");
        Assert.True(stand.MaxZ > stand.MinZ, "Stand should have positive Z extent");

        // Stand should be within village bounds
        var (vMinX, vMinZ, vMaxX, vMaxZ) = VillageLayout.GetVillageBounds(resourceCount);
        Assert.True(stand.MinX >= vMinX - 5, "Stand should be near village bounds");
        Assert.True(stand.MaxX <= vMaxX + 5, "Stand should be near village bounds");
    }

    // ====================================================================
    // INTEGRATION: BRIDGE SERVICE WITH REAL GEOMETRY
    // ====================================================================

    [Fact]
    public async Task BridgeService_WithResources_BuildsBridges()
    {
        // Validate that BridgeService can initialize without errors
        // (exercises the real BuildAllBridgesAsync path)
        await using var server = new MockRconServer();
        var rcon = new RconService("127.0.0.1", server.Port, "test",
            NullLogger<RconService>.Instance, maxCommandsPerSecond: 1000);
        var monitor = TestResourceMonitorFactory.Create();
        var protection = new BuildingProtectionService(NullLogger<BuildingProtectionService>.Instance);

        TestResourceMonitorFactory.SetResources(monitor,
            ("api", true), ("db", true), ("web", true), ("cache", true));

        try
        {
            // Warm up RCON
            for (int i = 0; i < 10; i++)
            {
                try { await rcon.SendCommandAsync("list"); break; }
                catch { await Task.Delay(100); }
            }

            var bridgeService = new BridgeService(rcon, monitor,
                NullLogger<BridgeService>.Instance);

            // Should not throw
            await bridgeService.InitializeAsync();

            // Verify at least some fill/setblock commands were issued
            var cmds = server.GetCommands();
            Assert.True(cmds.Count > 1, "Bridge initialization should issue RCON commands");
        }
        finally
        {
            await rcon.DisposeAsync();
        }
    }
}
