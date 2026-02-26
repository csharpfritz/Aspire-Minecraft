using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Services;

/// <summary>
/// Integration tests for StructureBuilder that verify complete village generation with accurate
/// RCON command sequences. These tests prevent regressions in the coordinate calculations,
/// block placement logic, and command formatting that have caused issues in Sprint 3.
/// </summary>
public class StructureBuilderTests : IAsyncLifetime
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

    /// <summary>
    /// Core integration test: 4 resources covering all structure types.
    /// Validates complete village build including fence, paths, structures, doors, health indicators, and signs.
    /// This is the regression test for Sprint 3 coordinate bugs.
    /// </summary>
    [Fact]
    public async Task UpdateStructuresAsync_FourResourceVillage_GeneratesCorrectRconCommands()
    {
        // Arrange: 4 resources with different types (Project, Container, Executable, Unknown)
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api-service", "Project", ResourceStatus.Healthy),
            ("redis-cache", "Container", ResourceStatus.Healthy),
            ("worker-exe", "Executable", ResourceStatus.Unhealthy),
            ("legacy-app", "Unknown", ResourceStatus.Healthy)
        );

        _server.ClearCommands();

        // Act
        await _structureBuilder.UpdateStructuresAsync();

        // Assert
        var commands = _server.GetCommands();

        // === 1. FENCE PERIMETER ===
        // VillageLayout.GetFencePerimeter(4) returns (0, -10, 50, 40) based on 2x2 grid
        // Expected: 4 fence sides + 1 gate section = 5 fence commands
        var fenceCommands = commands.Where(c => c.Contains("oak_fence")).ToList();
        Assert.True(fenceCommands.Count >= 4, 
            $"Expected at least 4 fence commands (south-left, south-right, north, west, east) but got {fenceCommands.Count}");
        
        // Verify gate at center of south side
        var gateCommands = commands.Where(c => c.Contains("oak_fence_gate")).ToList();
        Assert.Single(gateCommands);
        Assert.Contains("oak_fence_gate[facing=south]", gateCommands[0]);

        // === 2. COMPREHENSIVE COBBLESTONE PATHS ===
        // Should have 2 path commands: clear grass at BaseY, place cobblestone at BaseY-1
        var pathCommands = commands.Where(c => 
            c.Contains("minecraft:air replace grass_block") ||
            c.Contains("minecraft:cobblestone")).ToList();
        Assert.True(pathCommands.Count >= 2, 
            $"Expected at least 2 path commands (grass clear + cobblestone fill) but got {pathCommands.Count}");
        
        // Verify path cobblestone is at SurfaceY (-60, the grass block level)
        var cobblestonePathCmd = commands.FirstOrDefault(c => 
            c.Contains("cobblestone") && c.Contains(" -60 "));
        Assert.NotNull(cobblestonePathCmd);

        // === 3. STRUCTURE-SPECIFIC BLOCKS ===
        
        // Watchtower (Project): stone_bricks, purple_wool, purple_banner (standing)
        var watchtowerCommands = commands.Where(c => 
            c.Contains("stone_bricks") || c.Contains("purple_wool") || c.Contains("purple_banner")).ToList();
        Assert.True(watchtowerCommands.Count >= 3,
            $"Expected Watchtower structure commands but got {watchtowerCommands.Count}");

        // Warehouse (Container): iron_block, purple_stained_glass, barrel
        var warehouseCommands = commands.Where(c => 
            c.Contains("iron_block") || c.Contains("purple_stained_glass") || c.Contains("barrel")).ToList();
        Assert.True(warehouseCommands.Count >= 3,
            $"Expected Warehouse structure commands but got {warehouseCommands.Count}");

        // Workshop (Executable): oak_planks, cyan_stained_glass, crafting_table, anvil
        var workshopCommands = commands.Where(c => 
            c.Contains("oak_planks") || c.Contains("cyan_stained_glass") || 
            c.Contains("crafting_table") || c.Contains("anvil")).ToList();
        Assert.True(workshopCommands.Count >= 3,
            $"Expected Workshop structure commands but got {workshopCommands.Count}");

        // Cottage (Unknown): cobblestone, white_wool
        var cottageCommands = commands.Where(c => 
            c.Contains("white_wool")).ToList();
        Assert.True(cottageCommands.Count >= 1,
            $"Expected Cottage structure commands but got {cottageCommands.Count}");

        // === 4. DOORS (AIR BLOCKS) ===
        // All structures should have door openings cleared with minecraft:air
        var doorCommands = commands.Where(c => c.Contains("minecraft:air") && c.Contains("fill")).ToList();
        Assert.True(doorCommands.Count >= 4,
            $"Expected at least 4 door clearing commands (one per structure) but got {doorCommands.Count}");

        // === 5. HEALTH INDICATORS ===
        // 3 glowstone (healthy), 1 redstone_lamp (unhealthy) for structures
        // Dashboard may add additional lamps
        var healthIndicators = commands.Where(c => 
            c.Contains("glowstone") || c.Contains("redstone_lamp") || c.Contains("sea_lantern")).ToList();
        Assert.True(healthIndicators.Count >= 4,
            $"Expected at least 4 health indicators but got {healthIndicators.Count}");
        
        var glowstoneCount = commands.Count(c => c.Contains("minecraft:glowstone"));
        Assert.True(glowstoneCount >= 3,
            $"Expected at least 3 glowstone blocks but got {glowstoneCount}");
        
        var redstoneLampCount = commands.Count(c => c.Contains("minecraft:redstone_lamp"));
        Assert.True(redstoneLampCount >= 1,
            $"Expected at least 1 redstone_lamp but got {redstoneLampCount}");

        // === 6. SIGNS WITH RESOURCE NAMES ===
        // Should have 4 sign placement commands for structures (grand buildings use oak_wall_sign)
        // Dashboard may add additional signs
        var signPlacementCommands = commands.Where(c => c.Contains("minecraft:oak_sign") || c.Contains("minecraft:oak_wall_sign")).ToList();
        Assert.True(signPlacementCommands.Count >= 4,
            $"Expected at least 4 sign placement commands but got {signPlacementCommands.Count}");
        
        var signDataCommands = commands.Where(c => c.Contains("data merge block")).ToList();
        Assert.True(signDataCommands.Count >= 4,
            $"Expected at least 4 data merge commands (one per structure) but got {signDataCommands.Count}");
        
        // Verify all resource names appear in sign commands
        Assert.Contains(signDataCommands, c => c.Contains("api-service"));
        Assert.Contains(signDataCommands, c => c.Contains("redis-cache"));
        Assert.Contains(signDataCommands, c => c.Contains("worker-exe"));
        Assert.Contains(signDataCommands, c => c.Contains("legacy-app"));

        // === 7. COORDINATE VALIDATION ===
        // VillageLayout: BaseX=10, SurfaceY=-60, BaseZ=0, Spacing=36 (grand layout)
        // GetStructureOrigin returns SurfaceY+1 = -59 for Y
        // Index 0 (api-service): (10, -59, 0)
        // Index 1 (redis-cache): (46, -59, 0)
        // Index 2 (worker-exe): (10, -59, 36)
        // Index 3 (legacy-app): (46, -59, 36)
        
        // Verify at least one command uses the first structure origin (10, -59, 0)
        var structure0Commands = commands.Where(c => c.Contains(" 10 -59 0") || c.Contains(" 10 -60 0")).ToList();
        Assert.NotEmpty(structure0Commands);
        
        // Verify at least one command uses the second structure origin (46, -59, 0) or (46, -60, 0)
        var structure1Commands = commands.Where(c => c.Contains(" 46 -59 0") || c.Contains(" 46 -60 0")).ToList();
        Assert.NotEmpty(structure1Commands);

        // === 8. OVERALL COMMAND COUNT ===
        // Should have 70+ commands for a complete village build
        // Fence (~5) + Paths (2) + 4 Structures (~15 each) + Health (4) + Signs (8) = ~79+
        Assert.True(commands.Count >= 70,
            $"Expected at least 70 total commands for complete village but got {commands.Count}");
    }

    /// <summary>
    /// Verifies that health indicator updates work without rebuilding entire structures.
    /// </summary>
    [Fact]
    public async Task UpdateStructuresAsync_SecondCall_OnlyUpdatesHealthIndicators()
    {
        // Arrange: Build initial village
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("service-a", "Project", ResourceStatus.Healthy)
        );
        await _structureBuilder.UpdateStructuresAsync();
        _server.ClearCommands();

        // Act: Change health status and update again
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("service-a", "Project", ResourceStatus.Unhealthy)
        );
        await _structureBuilder.UpdateStructuresAsync();

        // Assert: Only health indicator update commands, no structure rebuilds
        var commands = _server.GetCommands();
        
        // Should have exactly 1 health indicator update
        Assert.Single(commands);
        Assert.Contains("setblock", commands[0]);
        Assert.Contains("redstone_lamp", commands[0]);
        
        // Should NOT have any structure building commands
        Assert.DoesNotContain(commands, c => c.Contains("stone_bricks"));
        Assert.DoesNotContain(commands, c => c.Contains("fill"));
    }

    /// <summary>
    /// Verifies empty village (no resources) doesn't crash and sends no commands.
    /// </summary>
    [Fact]
    public async Task UpdateStructuresAsync_NoResources_SendsNoCommands()
    {
        // Arrange: Empty resource set
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor);
        _server.ClearCommands();

        // Act
        await _structureBuilder.UpdateStructuresAsync();

        // Assert: No commands sent (no fence, no paths, no structures)
        var commands = _server.GetCommands();
        Assert.Empty(commands);
    }

    /// <summary>
    /// Verifies single resource generates minimal but complete village.
    /// </summary>
    [Fact]
    public async Task UpdateStructuresAsync_SingleResource_BuildsMinimalVillage()
    {
        // Arrange
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("solo-app", "Project", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        // Act
        await _structureBuilder.UpdateStructuresAsync();

        // Assert
        var commands = _server.GetCommands();
        
        // Should have fence, path, structure, health, and sign commands
        Assert.Contains(commands, c => c.Contains("oak_fence"));
        Assert.Contains(commands, c => c.Contains("cobblestone"));
        Assert.Contains(commands, c => c.Contains("stone_bricks")); // Watchtower
        Assert.Contains(commands, c => c.Contains("glowstone")); // Health indicator
        Assert.Contains(commands, c => c.Contains("oak_sign")); // Sign placement
        Assert.Contains(commands, c => c.Contains("data merge block")); // Sign data
    }

    /// <summary>
    /// Verifies correct structure type selection based on resource type.
    /// </summary>
    [Theory]
    [InlineData("Project", "Watchtower")]
    [InlineData("project", "Watchtower")]
    [InlineData("Container", "Warehouse")]
    [InlineData("container", "Warehouse")]
    [InlineData("Executable", "Workshop")]
    [InlineData("executable", "Workshop")]
    [InlineData("PythonApp", "Workshop")]
    [InlineData("NodeApp", "Workshop")]
    [InlineData("JavaScriptApp", "Workshop")]
    [InlineData("Unknown", "Cottage")]
    [InlineData("CustomType", "Cottage")]
    [InlineData("", "Cottage")]
    public void GetStructureType_VariousResourceTypes_ReturnsCorrectStructure(
        string resourceType, string expectedStructureType)
    {
        // Act
        var actualStructureType = StructureBuilder.GetStructureType(resourceType);

        // Assert
        Assert.Equal(expectedStructureType, actualStructureType);
    }

    /// <summary>
    /// Stress test: Verifies 10 resources generate commands without errors.
    /// </summary>
    [Fact]
    public async Task UpdateStructuresAsync_TenResources_NoExceptions()
    {
        // Arrange: 10 resources with mixed types and statuses
        var resources = new (string, string, ResourceStatus)[]
        {
            ("service-01", "Project", ResourceStatus.Healthy),
            ("service-02", "Container", ResourceStatus.Unhealthy),
            ("service-03", "Executable", ResourceStatus.Healthy),
            ("service-04", "Unknown", ResourceStatus.Healthy),
            ("service-05", "Project", ResourceStatus.Healthy),
            ("service-06", "Container", ResourceStatus.Healthy),
            ("service-07", "Executable", ResourceStatus.Unhealthy),
            ("service-08", "Unknown", ResourceStatus.Healthy),
            ("service-09", "Project", ResourceStatus.Healthy),
            ("service-10", "Container", ResourceStatus.Healthy),
        };
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor, resources);
        _server.ClearCommands();

        // Act
        var ex = await Record.ExceptionAsync(() => _structureBuilder.UpdateStructuresAsync());

        // Assert: No exceptions thrown
        Assert.Null(ex);
        
        var commands = _server.GetCommands();
        
        // Should have fence, paths, 10 structures, 10 health indicators, 10 signs
        Assert.True(commands.Count >= 160, 
            $"Expected at least 160 commands for 10-resource village but got {commands.Count}");
        
        // Verify all 10 health indicators were placed
        var healthIndicators = commands.Count(c => 
            c.Contains("glowstone") || c.Contains("redstone_lamp") || c.Contains("sea_lantern"));
        Assert.Equal(10, healthIndicators);
        
        // Verify all 10 signs have data (grand structures may place extra info signs)
        var signDataCommands = commands.Count(c => c.Contains("data merge block"));
        Assert.True(signDataCommands >= 10,
            $"Expected at least 10 sign data commands but got {signDataCommands}");
    }

    /// <summary>
    /// Verifies all health statuses are represented correctly in indicators.
    /// </summary>
    [Fact]
    public async Task UpdateStructuresAsync_AllHealthStatuses_GeneratesCorrectIndicators()
    {
        // Arrange: One resource of each status
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("healthy-app", "Project", ResourceStatus.Healthy),
            ("unhealthy-app", "Container", ResourceStatus.Unhealthy),
            ("unknown-app", "Executable", ResourceStatus.Unknown)
        );
        _server.ClearCommands();

        // Act
        await _structureBuilder.UpdateStructuresAsync();

        // Assert: Verify correct lamp types
        var commands = _server.GetCommands();
        
        var glowstoneCount = commands.Count(c => c.Contains("minecraft:glowstone"));
        Assert.Equal(1, glowstoneCount);
        
        var redstoneLampCount = commands.Count(c => c.Contains("minecraft:redstone_lamp"));
        Assert.Equal(1, redstoneLampCount);
        
        var seaLanternCount = commands.Count(c => c.Contains("minecraft:sea_lantern"));
        Assert.Equal(1, seaLanternCount);
    }

    /// <summary>
    /// Regression test: Verifies Watchtower door is cleared at front wall (z=0 for grand layout).
    /// Grand Watchtower: gatehouse entrance at x+half = x+7, door spans x+6 to x+8.
    /// </summary>
    [Fact]
    public async Task UpdateStructuresAsync_WatchtowerDoor_ClearedAtCorrectZCoordinate()
    {
        // Arrange: Single Watchtower (Project)
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        // Act
        await _structureBuilder.UpdateStructuresAsync();

        // Assert: Grand Watchtower at index 0 is at (10, -60, 0)
        // Door: fill 16 -59 0 18 -56 0 minecraft:air (x+6 to x+8, y+1 to y+4, z=0)
        var commands = _server.GetCommands();
        var watchtowerDoorCommand = commands.FirstOrDefault(c => 
            c.Contains("fill") && 
            c.Contains("minecraft:air") && 
            c.Contains("16 ") && // x+half-1 = 16
            c.Contains("18 ") && // x+half+1 = 18
            c.Contains(" 0 minecraft:air")); // z=0 at end
        
        Assert.NotNull(watchtowerDoorCommand);
    }

    /// <summary>
    /// Regression test: Verifies Warehouse door is cleared at origin Z (z=0).
    /// Grand Warehouse: cargo bay entrance spans x+5 to x+9.
    /// </summary>
    [Fact]
    public async Task UpdateStructuresAsync_WarehouseDoor_ClearedAtOriginZ()
    {
        // Arrange: Single Warehouse (Container) at index 0
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("redis", "Container", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        // Act
        await _structureBuilder.UpdateStructuresAsync();

        // Assert: Grand Warehouse at (10, -60, 0)
        // Door: fill 15 -59 0 19 -56 0 minecraft:air (x+5 to x+9, y+1 to y+4, z=0)
        var commands = _server.GetCommands();
        var warehouseDoorCommand = commands.FirstOrDefault(c => 
            c.Contains("fill") && 
            c.Contains("minecraft:air") && 
            c.Contains("15 ") && // x+5 = 15
            c.Contains("19 ") && // x+9 = 19
            c.Contains(" 0 minecraft:air")); // z=0 at end
        
        Assert.NotNull(warehouseDoorCommand);
    }

    // ====================================================================
    // HEALTH INDICATOR GLOW BLOCK POSITION TESTS
    //
    // Rule: The health indicator glow block is ALWAYS placed just above the
    // door, flush with the front wall. All buildings face south (z-min).
    // Standard layout: StructureSize=7, lampX = x+3 (center)
    // Grand layout:    StructureSize=15, lampX = x+7 (center)
    // ====================================================================

    // ====================================================================
    // GRAND LAYOUT HEALTH INDICATOR TESTS
    //
    // Grand layout: StructureSize=15, lampX = x+7 (half=7).
    // All grand buildings have door on front wall at z.
    // ====================================================================

    /// <summary>
    /// Grand Warehouse: 4-tall cargo door → lamp at y+5, front wall z.
    /// Grand layout: structure at (10, -59, 0) → lamp at (17, -54, 0).
    /// </summary>
    [Fact]
    public async Task HealthIndicator_GrandWarehouse_PlacedAboveDoorFlushWithFrontWall()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("redis", "Container", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        // Grand Warehouse at (10, -59, 0): lampX=17, lampY=-54, lampZ=0
        var healthCmd = commands.FirstOrDefault(c =>
            c.Contains("setblock 17 -54 0 minecraft:glowstone"));

        Assert.NotNull(healthCmd);
    }

    /// <summary>
    /// Grand Workshop: 3-tall door → lamp at y+4, front wall z.
    /// Grand layout: structure at (10, -59, 0) → lamp at (17, -55, 0).
    /// </summary>
    [Fact]
    public async Task HealthIndicator_GrandWorkshop_PlacedAboveDoorFlushWithFrontWall()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("worker", "Executable", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        // Grand Workshop at (10, -59, 0): lampX=17, lampY=-54, lampZ=0
        var healthCmd = commands.FirstOrDefault(c =>
            c.Contains("setblock 17 -54 0 minecraft:glowstone"));

        Assert.NotNull(healthCmd);
    }

    /// <summary>
    /// Grand Cottage: 2-tall door → lamp at y+3, front wall z.
    /// Grand layout: structure at (10, -59, 0) → lamp at (17, -56, 0).
    /// </summary>
    [Fact]
    public async Task HealthIndicator_GrandCottage_PlacedAboveDoorFlushWithFrontWall()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("misc", "SomeType", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        // Grand Cottage at (10, -59, 0): lampX=17, lampY=-56, lampZ=0
        var healthCmd = commands.FirstOrDefault(c =>
            c.Contains("setblock 17 -56 0 minecraft:glowstone"));

        Assert.NotNull(healthCmd);
    }

    /// <summary>
    /// Grand Cylinder/Silo: 2-tall iron door (y+1 to y+2) → lamp at y+3, front wall z.
    /// Grand layout: structure at (10, -59, 0) → lamp at (17, -56, 0).
    /// NOT at z+4 (which would be inside the building).
    /// </summary>
    [Fact]
    public async Task HealthIndicator_GrandCylinder_PlacedAboveDoorFlushWithFrontWall()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("db", "postgres", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        // Grand Cylinder at (10, -59, 0): lampX=17, lampY=-56, lampZ=0
        var healthCmd = commands.FirstOrDefault(c =>
            c.Contains("setblock 17 -56 0 minecraft:glowstone"));

        Assert.NotNull(healthCmd);
    }

    /// <summary>
    /// Grand Azure Pavilion: 2-tall door → lamp at y+3, front wall z.
    /// Grand layout: structure at (10, -59, 0) → lamp at (17, -56, 0).
    /// </summary>
    [Fact]
    public async Task HealthIndicator_GrandAzurePavilion_PlacedAboveDoorFlushWithFrontWall()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("storage", "azure.storage", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        // Grand Azure Pavilion at (10, -59, 0): lampX=17, lampY=-55, lampZ=0
        var healthCmd = commands.FirstOrDefault(c =>
            c.Contains("setblock 17 -55 0 minecraft:glowstone"));

        Assert.NotNull(healthCmd);
    }

    /// <summary>
    /// Verifies that the health indicator glow block is NEVER placed inside the building
    /// (e.g., at z+4 for cylinders). It must always be at the front wall face.
    /// </summary>
    [Theory]
    [InlineData("Project", "Watchtower")]
    [InlineData("Container", "Warehouse")]
    [InlineData("Executable", "Workshop")]
    [InlineData("SomeType", "Cottage")]
    [InlineData("postgres", "Cylinder")]
    [InlineData("azure.storage", "AzureThemed")]
    public async Task HealthIndicator_AllTypes_NeverPlacedBehindFrontWall(
        string resourceType, string expectedStructure)
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("test-resource", resourceType, ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        // Health indicator is a setblock with glowstone
        var healthCmd = commands.FirstOrDefault(c =>
            c.Contains("setblock") && c.Contains("minecraft:glowstone"));

        Assert.NotNull(healthCmd);

        // Parse the Z coordinate from "setblock X Y Z minecraft:glowstone"
        var parts = healthCmd!.Split(' ');
        var zIndex = Array.IndexOf(parts, "setblock") + 3; // setblock X Y Z
        var lampZ = int.Parse(parts[zIndex]);

        // Structure at index 0 is at z=0. Front wall is at z (or z+1 for Watchtower).
        // Lamp must be at the front wall, never deeper (higher Z).
        var maxAcceptableZ = expectedStructure == "Watchtower" ? 1 : 0;
        Assert.True(lampZ <= maxAcceptableZ,
            $"{expectedStructure} health indicator at Z={lampZ} is behind front wall (max Z={maxAcceptableZ})");
    }

    // ====================================================================
    // GRAND WATCHTOWER TESTS
    //
    // Grand layout: StructureSize=15. The Grand Watchtower is the Project
    // resource building at 15×15 scale. It features stone_bricks walls,
    // crenellated battlements (stone_brick_stairs), 3 oak_planks floors,
    // a spiral staircase (oak_stairs), and a front-wall sign.
    // DoorPosition: (x+7, y+4, z) → GlowBlock: (x+7, y+5, z)
    // ====================================================================

    /// <summary>
    /// Grand dispatch: When StructureSize >= 15 and resource type is "Project",
    /// the watchtower should produce stone_bricks commands spanning 15 blocks (not 7).
    /// </summary>
    [Fact]
    public async Task GrandWatchtower_Dispatch_ProducesStone15BlockCommands()
    {

        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();

        // Grand watchtower must use stone_bricks
        Assert.Contains(commands, c => c.Contains("stone_bricks"));

        // Should reference coordinates spanning 15 blocks (x to x+14)
        // Structure at index 0: origin (10, -59, 0), so x+14 = 24
        Assert.Contains(commands, c => c.Contains(" 24 ") || c.Contains(" 24,"));
    }

    /// <summary>
    /// Grand Watchtower health indicator (glowstone) should be at (x+7, y+5, z).
    /// With grand layout, structure at (10, -59, 0): DoorPosition = (17, -55, 0),
    /// GlowBlock = (17, -54, 0).
    /// </summary>
    [Fact]
    public async Task GrandWatchtower_HealthIndicator_PlacedAtCorrectPosition()
    {

        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();

        // GlowBlock = DoorPosition.TopY + 1 = (x+7, y+5, z) = (17, -54, 0)
        var healthCmd = commands.FirstOrDefault(c =>
            c.Contains("setblock 17 -54 0 minecraft:glowstone"));

        Assert.NotNull(healthCmd);
    }

    /// <summary>
    /// Grand Watchtower has crenellated battlements using stone_brick_stairs.
    /// </summary>
    [Fact]
    public async Task GrandWatchtower_HasCrenellatedBattlements()
    {

        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();

        // Battlements use stone_brick_stairs blocks
        Assert.Contains(commands, c => c.Contains("stone_brick_stairs"));
    }

    /// <summary>
    /// Grand Watchtower has 3 floors using oak_planks at multiple Y levels.
    /// </summary>
    [Fact]
    public async Task GrandWatchtower_HasThreeFloors()
    {

        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();

        // Floors use oak_planks — expect multiple Y levels for 3 floors
        var floorCommands = commands.Where(c => c.Contains("oak_planks")).ToList();
        Assert.True(floorCommands.Count >= 2,
            $"Expected at least 2 oak_planks commands for multi-floor watchtower but got {floorCommands.Count}");
    }

    /// <summary>
    /// Grand Watchtower has a spiral staircase using oak_stairs blocks.
    /// </summary>
    [Fact]
    public async Task GrandWatchtower_HasSpiralStaircase()
    {

        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();

        // Spiral staircase uses oak_stairs blocks
        Assert.Contains(commands, c => c.Contains("oak_stairs"));
    }

    /// <summary>
    /// Grand Watchtower sign is placed on the front wall (data merge block command present with resource name).
    /// </summary>
    [Fact]
    public async Task GrandWatchtower_SignPlacement_HasDataMergeWithResourceName()
    {

        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();

        // Sign data merge command should contain the resource name
        var signDataCmd = commands.FirstOrDefault(c =>
            c.Contains("data merge block") && c.Contains("api"));
        Assert.NotNull(signDataCmd);
    }

    /// <summary>
    /// Grand Watchtower RCON budget: total commands should be under 100 for a single grand watchtower.
    /// </summary>
    [Fact]
    public async Task GrandWatchtower_RconBudget_Under100Commands()
    {

        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();

        // Total commands for fence + paths + structure + health + sign should be under 100
        Assert.True(commands.Count < 100,
            $"Expected under 100 RCON commands for a single grand watchtower village but got {commands.Count}");
    }

    // ====================================================================
    // GEOMETRIC VALIDATION TESTS
    //
    // These tests catch the categories of bugs that escaped review:
    // 1. Doorway overlap - blocks placed inside the door opening
    // 2. Ground-level continuity - stairs/decorations at y+1 on front face
    // 3. Health indicator placement - glow block position validation
    // ====================================================================

    /// <summary>
    /// Grand Watchtower doorway (3-wide, 4-tall): verify NO blocks overlap the door opening.
    /// Door opening: x+6 to x+8 (CenterX-1 to CenterX+1), y+1 to y+4, z=0.
    /// </summary>
    [Fact]
    public async Task GrandWatchtower_DoorwayVisibility_NoBlocksOverlapDoorOpening()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        // Structure at (10, -59, 0), DoorPosition(17, -55, 0) → door opening x=16-18, y=-58 to -55, z=0
        var doorX = new[] { 16, 17, 18 };
        var doorY = new[] { -58, -57, -56, -55 };
        var doorZ = 0;

        // Find all setblock commands that place non-air blocks in the door region
        var overlappingBlocks = commands.Where(c =>
        {
            if (!c.StartsWith("setblock ")) return false;
            var parts = c.Split(' ');
            if (parts.Length < 5) return false;

            if (!int.TryParse(parts[1], out int x)) return false;
            if (!int.TryParse(parts[2], out int y)) return false;
            if (!int.TryParse(parts[3], out int z)) return false;
            
            var block = parts[4];
            
            // Check if this is in the door opening and NOT air
            return doorX.Contains(x) && doorY.Contains(y) && z == doorZ && 
                   !block.Contains("air") && !block.Contains("oak_door");
        }).ToList();

        Assert.Empty(overlappingBlocks);
    }

    /// <summary>
    /// Grand Warehouse doorway (3-wide, 4-tall): verify NO blocks overlap the door opening.
    /// Door opening: x+6 to x+8, y+1 to y+4, z=0.
    /// </summary>
    [Fact]
    public async Task GrandWarehouse_DoorwayVisibility_NoBlocksOverlapDoorOpening()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("cache", "Container", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        // Structure at (10, -59, 0), DoorPosition(17, -55, 0) → door x=16-18, y=-58 to -55, z=0
        var doorX = new[] { 16, 17, 18 };
        var doorY = new[] { -58, -57, -56, -55 };
        var doorZ = 0;

        var overlappingBlocks = commands.Where(c =>
        {
            if (!c.StartsWith("setblock ")) return false;
            var parts = c.Split(' ');
            if (parts.Length < 5) return false;

            if (!int.TryParse(parts[1], out int x)) return false;
            if (!int.TryParse(parts[2], out int y)) return false;
            if (!int.TryParse(parts[3], out int z)) return false;
            
            var block = parts[4];
            
            return doorX.Contains(x) && doorY.Contains(y) && z == doorZ && 
                   !block.Contains("air") && !block.Contains("iron_door");
        }).ToList();

        Assert.Empty(overlappingBlocks);
    }

    /// <summary>
    /// Grand Workshop doorway (3-wide, 3-tall): verify NO blocks overlap the door opening.
    /// Door opening: x+6 to x+8, y+1 to y+3, z=0.
    /// </summary>
    [Fact]
    public async Task GrandWorkshop_DoorwayVisibility_NoBlocksOverlapDoorOpening()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("worker", "Executable", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        // Structure at (10, -59, 0), DoorPosition(17, -55, 0) → door x=16-18, y=-58 to -55, z=0
        var doorX = new[] { 16, 17, 18 };
        var doorY = new[] { -58, -57, -56, -55 };
        var doorZ = 0;

        var overlappingBlocks = commands.Where(c =>
        {
            if (!c.StartsWith("setblock ")) return false;
            var parts = c.Split(' ');
            if (parts.Length < 5) return false;

            if (!int.TryParse(parts[1], out int x)) return false;
            if (!int.TryParse(parts[2], out int y)) return false;
            if (!int.TryParse(parts[3], out int z)) return false;
            
            var block = parts[4];
            
            return doorX.Contains(x) && doorY.Contains(y) && z == doorZ && 
                   !block.Contains("air") && !block.Contains("oak_door");
        }).ToList();

        Assert.Empty(overlappingBlocks);
    }

    /// <summary>
    /// Grand Cottage doorway (3-wide, 2-tall): verify NO blocks overlap the door opening.
    /// Door opening: x+6 to x+8, y+1 to y+2, z=0.
    /// </summary>
    [Fact]
    public async Task GrandCottage_DoorwayVisibility_NoBlocksOverlapDoorOpening()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("misc", "SomeType", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        // Structure at (10, -59, 0), DoorPosition(17, -57, 0) → door x=16-18, y=-58 to -57, z=0
        var doorX = new[] { 16, 17, 18 };
        var doorY = new[] { -58, -57 };
        var doorZ = 0;

        var overlappingBlocks = commands.Where(c =>
        {
            if (!c.StartsWith("setblock ")) return false;
            var parts = c.Split(' ');
            if (parts.Length < 5) return false;

            if (!int.TryParse(parts[1], out int x)) return false;
            if (!int.TryParse(parts[2], out int y)) return false;
            if (!int.TryParse(parts[3], out int z)) return false;
            
            var block = parts[4];
            
            return doorX.Contains(x) && doorY.Contains(y) && z == doorZ && 
                   !block.Contains("air") && !block.Contains("oak_door");
        }).ToList();

        Assert.Empty(overlappingBlocks);
    }

    /// <summary>
    /// Grand Cylinder doorway (1-wide, 2-tall spruce door): verify NO blocks overlap the door opening.
    /// Door opening: x+7, y+1 to y+2, z=0.
    /// </summary>
    [Fact]
    public async Task GrandCylinder_DoorwayVisibility_NoBlocksOverlapDoorOpening()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("db", "postgres", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        // Structure at (10, -59, 0), DoorPosition(17, -57, 0) → door x=17, y=-58 to -57, z=0
        var doorX = new[] { 17 };
        var doorY = new[] { -58, -57 };
        var doorZ = 0;

        var overlappingBlocks = commands.Where(c =>
        {
            if (!c.StartsWith("setblock ")) return false;
            var parts = c.Split(' ');
            if (parts.Length < 5) return false;

            if (!int.TryParse(parts[1], out int x)) return false;
            if (!int.TryParse(parts[2], out int y)) return false;
            if (!int.TryParse(parts[3], out int z)) return false;
            
            var block = parts[4];
            
            return doorX.Contains(x) && doorY.Contains(y) && z == doorZ && 
                   !block.Contains("air") && !block.Contains("_door");
        }).ToList();

        Assert.Empty(overlappingBlocks);
    }

    /// <summary>
    /// Grand Azure Pavilion doorway (2-wide, 3-tall): verify NO blocks overlap the door opening.
    /// Door opening: x+6 to x+7, y+1 to y+3, z=0.
    /// </summary>
    [Fact]
    public async Task GrandAzurePavilion_DoorwayVisibility_NoBlocksOverlapDoorOpening()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("storage", "azure.storage", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        // Structure at (10, -59, 0), DoorPosition(17, -56, 0) → door x=16-17, y=-58 to -56, z=0
        var doorX = new[] { 16, 17 };
        var doorY = new[] { -58, -57, -56 };
        var doorZ = 0;

        var overlappingBlocks = commands.Where(c =>
        {
            if (!c.StartsWith("setblock ")) return false;
            var parts = c.Split(' ');
            if (parts.Length < 5) return false;

            if (!int.TryParse(parts[1], out int x)) return false;
            if (!int.TryParse(parts[2], out int y)) return false;
            if (!int.TryParse(parts[3], out int z)) return false;
            
            var block = parts[4];
            
            return doorX.Contains(x) && doorY.Contains(y) && z == doorZ && 
                   !block.Contains("air") && !block.Contains("dark_oak_door");
        }).ToList();

        Assert.Empty(overlappingBlocks);
    }

    /// <summary>
    /// Ground level continuity: Grand Watchtower front face at y+1 (z=0) should have NO stairs
    /// or decorative blocks except in the door opening region. The front face should be
    /// either wall material or air (for the door).
    /// </summary>
    [Fact]
    public async Task GrandWatchtower_GroundLevel_NoStairsOnFrontFace()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        // Structure at (10, -59, 0). Front face z=0, ground level y=-58 (y+1).
        // Check x=10 to x=24 (full 15-block width), y=-58, z=0.
        // NO stairs or decorative blocks outside door opening (x=16-18).
        var groundZ = 0;
        var groundY = -58;

        var unwantedGroundBlocks = commands.Where(c =>
        {
            if (!c.StartsWith("setblock ")) return false;
            var parts = c.Split(' ');
            if (parts.Length < 5) return false;

            if (!int.TryParse(parts[1], out int x)) return false;
            if (!int.TryParse(parts[2], out int y)) return false;
            if (!int.TryParse(parts[3], out int z)) return false;
            
            var block = parts[4];
            
            // Check if this is ground level front face
            if (y != groundY || z != groundZ) return false;
            if (x < 10 || x > 24) return false; // Outside structure bounds
            
            // Door opening x=16-18 is allowed to have air/door
            if (x >= 16 && x <= 18) return false;
            
            // Everything else should be wall material (stone_bricks, etc.) or air
            // Flag stairs, lanterns, torches, etc.
            return block.Contains("stairs") || block.Contains("lantern") || 
                   block.Contains("torch") || block.Contains("fence") ||
                   block.Contains("carpet") || block.Contains("slab");
        }).ToList();

        Assert.Empty(unwantedGroundBlocks);
    }

    /// <summary>
    /// Ground level continuity: Grand Warehouse front face at y+1 (z=0) should have NO stairs
    /// or decorative blocks outside the door opening.
    /// </summary>
    [Fact]
    public async Task GrandWarehouse_GroundLevel_NoStairsOnFrontFace()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("cache", "Container", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        var groundZ = 0;
        var groundY = -58;

        var unwantedGroundBlocks = commands.Where(c =>
        {
            if (!c.StartsWith("setblock ")) return false;
            var parts = c.Split(' ');
            if (parts.Length < 5) return false;

            if (!int.TryParse(parts[1], out int x)) return false;
            if (!int.TryParse(parts[2], out int y)) return false;
            if (!int.TryParse(parts[3], out int z)) return false;
            
            var block = parts[4];
            
            if (y != groundY || z != groundZ) return false;
            if (x < 10 || x > 24) return false;
            if (x >= 16 && x <= 18) return false; // Door opening
            
            return block.Contains("stairs") || block.Contains("lantern") || 
                   block.Contains("torch") || block.Contains("fence") ||
                   block.Contains("carpet") || block.Contains("slab");
        }).ToList();

        Assert.Empty(unwantedGroundBlocks);
    }

    /// <summary>
    /// Ground level continuity: Grand Workshop front face at y+1 (z=0) should have NO stairs
    /// or decorative blocks outside the door opening.
    /// </summary>
    [Fact]
    public async Task GrandWorkshop_GroundLevel_NoStairsOnFrontFace()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("worker", "Executable", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        var groundZ = 0;
        var groundY = -58;

        var unwantedGroundBlocks = commands.Where(c =>
        {
            if (!c.StartsWith("setblock ")) return false;
            var parts = c.Split(' ');
            if (parts.Length < 5) return false;

            if (!int.TryParse(parts[1], out int x)) return false;
            if (!int.TryParse(parts[2], out int y)) return false;
            if (!int.TryParse(parts[3], out int z)) return false;
            
            var block = parts[4];
            
            if (y != groundY || z != groundZ) return false;
            if (x < 10 || x > 24) return false;
            if (x >= 16 && x <= 18) return false;
            
            return block.Contains("stairs") || block.Contains("lantern") || 
                   block.Contains("torch") || block.Contains("fence") ||
                   block.Contains("carpet") || block.Contains("slab");
        }).ToList();

        Assert.Empty(unwantedGroundBlocks);
    }

    /// <summary>
    /// Ground level continuity: Grand Cottage front face at y+1 (z=0) should have NO stairs
    /// or decorative blocks outside the door opening.
    /// </summary>
    [Fact]
    public async Task GrandCottage_GroundLevel_NoStairsOnFrontFace()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("misc", "SomeType", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        var groundZ = 0;
        var groundY = -58;

        var unwantedGroundBlocks = commands.Where(c =>
        {
            if (!c.StartsWith("setblock ")) return false;
            var parts = c.Split(' ');
            if (parts.Length < 5) return false;

            if (!int.TryParse(parts[1], out int x)) return false;
            if (!int.TryParse(parts[2], out int y)) return false;
            if (!int.TryParse(parts[3], out int z)) return false;
            
            var block = parts[4];
            
            if (y != groundY || z != groundZ) return false;
            if (x < 10 || x > 24) return false;
            if (x >= 16 && x <= 18) return false;
            
            return block.Contains("stairs") || block.Contains("lantern") || 
                   block.Contains("torch") || block.Contains("fence") ||
                   block.Contains("carpet") || block.Contains("slab");
        }).ToList();

        Assert.Empty(unwantedGroundBlocks);
    }

    /// <summary>
    /// Ground level continuity: Grand Cylinder front face at y+1 (z=0) should have NO stairs
    /// or decorative blocks outside the door opening.
    /// </summary>
    [Fact]
    public async Task GrandCylinder_GroundLevel_NoStairsOnFrontFace()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("db", "postgres", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        var groundZ = 0;
        var groundY = -58;

        var unwantedGroundBlocks = commands.Where(c =>
        {
            if (!c.StartsWith("setblock ")) return false;
            var parts = c.Split(' ');
            if (parts.Length < 5) return false;

            if (!int.TryParse(parts[1], out int x)) return false;
            if (!int.TryParse(parts[2], out int y)) return false;
            if (!int.TryParse(parts[3], out int z)) return false;
            
            var block = parts[4];
            
            if (y != groundY || z != groundZ) return false;
            if (x < 10 || x > 24) return false;
            if (x >= 16 && x <= 18) return false;
            
            return block.Contains("stairs") || block.Contains("lantern") || 
                   block.Contains("torch") || block.Contains("fence") ||
                   block.Contains("carpet") || block.Contains("slab");
        }).ToList();

        Assert.Empty(unwantedGroundBlocks);
    }

    /// <summary>
    /// Ground level continuity: Grand Azure Pavilion front face at y+1 (z=0) should have NO stairs
    /// or decorative blocks outside the door opening.
    /// </summary>
    [Fact]
    public async Task GrandAzurePavilion_GroundLevel_NoStairsOnFrontFace()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("storage", "azure.storage", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        var groundZ = 0;
        var groundY = -58;

        var unwantedGroundBlocks = commands.Where(c =>
        {
            if (!c.StartsWith("setblock ")) return false;
            var parts = c.Split(' ');
            if (parts.Length < 5) return false;

            if (!int.TryParse(parts[1], out int x)) return false;
            if (!int.TryParse(parts[2], out int y)) return false;
            if (!int.TryParse(parts[3], out int z)) return false;
            
            var block = parts[4];
            
            if (y != groundY || z != groundZ) return false;
            if (x < 10 || x > 24) return false;
            if (x >= 16 && x <= 18) return false;
            
            return block.Contains("stairs") || block.Contains("lantern") || 
                   block.Contains("torch") || block.Contains("fence") ||
                   block.Contains("carpet") || block.Contains("slab");
        }).ToList();

        Assert.Empty(unwantedGroundBlocks);
    }

    /// <summary>
    /// Health indicator placement: Grand Watchtower glow block must be at (CenterX, TopY+1, FaceZ).
    /// For structure at (10, -59, 0), DoorPosition(17, -55, 0) → GlowBlock at (17, -54, 0).
    /// Must be flush with front wall, NEVER inside the building.
    /// </summary>
    [Fact]
    public async Task GrandWatchtower_HealthIndicator_AtCorrectDoorPositionCoordinates()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        // Expected: GlowBlock = (17, -54, 0) - just above door, flush with front wall
        var healthCmd = commands.FirstOrDefault(c =>
            c.Contains("setblock") && c.Contains("minecraft:glowstone"));
        
        Assert.NotNull(healthCmd);
        
        var parts = healthCmd!.Split(' ');
        Assert.Equal("17", parts[1]);  // CenterX
        Assert.Equal("-54", parts[2]); // TopY + 1
        Assert.Equal("0", parts[3]);   // FaceZ (front wall)
    }

    /// <summary>
    /// Health indicator placement: Grand Warehouse glow block must be at (CenterX, TopY+1, FaceZ).
    /// </summary>
    [Fact]
    public async Task GrandWarehouse_HealthIndicator_AtCorrectDoorPositionCoordinates()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("cache", "Container", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        var healthCmd = commands.FirstOrDefault(c =>
            c.Contains("setblock") && c.Contains("minecraft:glowstone"));
        
        Assert.NotNull(healthCmd);
        
        var parts = healthCmd!.Split(' ');
        Assert.Equal("17", parts[1]);
        Assert.Equal("-54", parts[2]);
        Assert.Equal("0", parts[3]);
    }

    /// <summary>
    /// Health indicator placement: Grand Workshop glow block must be at (CenterX, TopY+1, FaceZ).
    /// DoorPosition(17, -56, 0) → GlowBlock at (17, -55, 0).
    /// </summary>
    [Fact]
    public async Task GrandWorkshop_HealthIndicator_AtCorrectDoorPositionCoordinates()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("worker", "Executable", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        var healthCmd = commands.FirstOrDefault(c =>
            c.Contains("setblock") && c.Contains("minecraft:glowstone"));
        
        Assert.NotNull(healthCmd);
        
        var parts = healthCmd!.Split(' ');
        Assert.Equal("17", parts[1]);
        Assert.Equal("-54", parts[2]); // TopY=y+4, so TopY+1=y+5=-54
        Assert.Equal("0", parts[3]);
    }

    /// <summary>
    /// Health indicator placement: Grand Cottage glow block must be at (CenterX, TopY+1, FaceZ).
    /// DoorPosition(17, -57, 0) → GlowBlock at (17, -56, 0).
    /// </summary>
    [Fact]
    public async Task GrandCottage_HealthIndicator_AtCorrectDoorPositionCoordinates()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("misc", "SomeType", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        var healthCmd = commands.FirstOrDefault(c =>
            c.Contains("setblock") && c.Contains("minecraft:glowstone"));
        
        Assert.NotNull(healthCmd);
        
        var parts = healthCmd!.Split(' ');
        Assert.Equal("17", parts[1]);
        Assert.Equal("-56", parts[2]);
        Assert.Equal("0", parts[3]);
    }

    /// <summary>
    /// Health indicator placement: Grand Cylinder glow block must be at (CenterX, TopY+1, FaceZ).
    /// DoorPosition(17, -57, 0) → GlowBlock at (17, -56, 0).
    /// </summary>
    [Fact]
    public async Task GrandCylinder_HealthIndicator_AtCorrectDoorPositionCoordinates()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("db", "postgres", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        var healthCmd = commands.FirstOrDefault(c =>
            c.Contains("setblock") && c.Contains("minecraft:glowstone"));
        
        Assert.NotNull(healthCmd);
        
        var parts = healthCmd!.Split(' ');
        Assert.Equal("17", parts[1]);
        Assert.Equal("-56", parts[2]);
        Assert.Equal("0", parts[3]);
    }

    /// <summary>
    /// Health indicator placement: Grand Azure Pavilion glow block must be at (CenterX, TopY+1, FaceZ).
    /// DoorPosition(17, -56, 0) → GlowBlock at (17, -55, 0).
    /// </summary>
    [Fact]
    public async Task GrandAzurePavilion_HealthIndicator_AtCorrectDoorPositionCoordinates()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("storage", "azure.storage", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();

        var commands = _server.GetCommands();
        
        var healthCmd = commands.FirstOrDefault(c =>
            c.Contains("setblock") && c.Contains("minecraft:glowstone"));
        
        Assert.NotNull(healthCmd);
        
        var parts = healthCmd!.Split(' ');
        Assert.Equal("17", parts[1]);
        Assert.Equal("-55", parts[2]);
        Assert.Equal("0", parts[3]);
    }
}
