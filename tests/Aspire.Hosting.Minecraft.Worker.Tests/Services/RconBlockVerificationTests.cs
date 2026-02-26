using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Services;

/// <summary>
/// Verifies that critical blocks (corners, door frames, glow blocks, floors)
/// are placed at the exact expected coordinates for each building type.
/// Uses MockRconServer to capture RCON commands and parse coordinates.
/// </summary>
public class RconBlockVerificationTests : IAsyncLifetime
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
    /// Builds a single structure and returns the captured commands.
    /// </summary>
    private async Task<List<string>> BuildStructure(string name, string type, bool grand = false)
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            (name, type, ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await _structureBuilder.UpdateStructuresAsync();
        return _server.GetCommands();
    }

    /// <summary>
    /// Asserts that a fill command exists covering the specified coordinates with the given block.
    /// </summary>
    private static void AssertFillCoversBlock(List<string> commands, int x, int y, int z, string blockType, string description)
    {
        var found = commands.Any(c =>
        {
            if (!c.StartsWith("fill ")) return false;
            var parts = c.Split(' ');
            if (parts.Length < 8) return false;
            if (!parts[7].Contains(blockType)) return false;

            if (!int.TryParse(parts[1], out int x1)) return false;
            if (!int.TryParse(parts[2], out int y1)) return false;
            if (!int.TryParse(parts[3], out int z1)) return false;
            if (!int.TryParse(parts[4], out int x2)) return false;
            if (!int.TryParse(parts[5], out int y2)) return false;
            if (!int.TryParse(parts[6], out int z2)) return false;

            return x >= Math.Min(x1, x2) && x <= Math.Max(x1, x2)
                && y >= Math.Min(y1, y2) && y <= Math.Max(y1, y2)
                && z >= Math.Min(z1, z2) && z <= Math.Max(z1, z2);
        });

        Assert.True(found, $"{description}: Expected {blockType} at ({x},{y},{z}) in a fill command");
    }

    /// <summary>
    /// Asserts that a setblock command exists at the specified coordinates with the given block.
    /// </summary>
    private static void AssertSetblock(List<string> commands, int x, int y, int z, string blockType, string description)
    {
        var found = commands.Any(c =>
            c.StartsWith("setblock ") && c.Contains($"{x} {y} {z}") && c.Contains(blockType));

        Assert.True(found, $"{description}: Expected setblock {blockType} at ({x},{y},{z})");
    }

    /// <summary>
    /// Asserts that a fill-air (door clearing) command covers the specified coordinate.
    /// </summary>
    private static void AssertDoorCleared(List<string> commands, int x, int y, int z, string description)
    {
        var found = commands.Any(c =>
        {
            if (!c.StartsWith("fill ") || !c.Contains("minecraft:air")) return false;
            var parts = c.Split(' ');
            if (parts.Length < 8) return false;

            if (!int.TryParse(parts[1], out int x1)) return false;
            if (!int.TryParse(parts[2], out int y1)) return false;
            if (!int.TryParse(parts[3], out int z1)) return false;
            if (!int.TryParse(parts[4], out int x2)) return false;
            if (!int.TryParse(parts[5], out int y2)) return false;
            if (!int.TryParse(parts[6], out int z2)) return false;

            return x >= Math.Min(x1, x2) && x <= Math.Max(x1, x2)
                && y >= Math.Min(y1, y2) && y <= Math.Max(y1, y2)
                && z >= Math.Min(z1, z2) && z <= Math.Max(z1, z2);
        });

        Assert.True(found, $"{description}: Expected air block at ({x},{y},{z})");
    }

    // ====================================================================
    // GRAND (15×15) BUILDING BLOCK VERIFICATION
    // Structure origin at index 0: (10, -59, 0)
    // ====================================================================

    [Fact]
    public async Task Grand_Watchtower_CornerBlocks()
    {
        var commands = await BuildStructure("api", "Project", grand: true);
        int x = 10, y = -59, z = 0;

        // Foundation — mossy_stone_bricks plinth
        AssertFillCoversBlock(commands, x, y, z, "mossy_stone_bricks", "SW corner");
        AssertFillCoversBlock(commands, x + 14, y, z, "mossy_stone_bricks", "SE corner");
        AssertFillCoversBlock(commands, x, y, z + 14, "mossy_stone_bricks", "NW corner");
        AssertFillCoversBlock(commands, x + 14, y, z + 14, "mossy_stone_bricks", "NE corner");
    }

    [Fact]
    public async Task Grand_Watchtower_DoorFrame()
    {
        var commands = await BuildStructure("api", "Project", grand: true);
        int x = 10, y = -59, z = 0;
        int half = 7;

        // Gatehouse door: x+half-1 to x+half+1, y+1 to y+4, z=0
        AssertDoorCleared(commands, x + half, y + 1, z, "Door center bottom");
        AssertDoorCleared(commands, x + half, y + 4, z, "Door center top");
    }

    [Fact]
    public async Task Grand_Watchtower_GlowBlock()
    {
        var commands = await BuildStructure("api", "Project", grand: true);
        // DoorPosition(x+7, y+4, z) → GlowBlock(17, -54, 0)
        AssertSetblock(commands, 17, -54, 0, "glowstone", "Health indicator");
    }

    [Fact]
    public async Task Grand_Watchtower_FloorSpansFootprint()
    {
        var commands = await BuildStructure("api", "Project", grand: true);
        int x = 10, y = -59, z = 0;
        AssertFillCoversBlock(commands, x + 7, y, z + 7, "mossy_stone_bricks", "Floor center");
    }

    [Fact]
    public async Task Grand_Warehouse_CornerBlocks()
    {
        var commands = await BuildStructure("redis", "Container", grand: true);
        int x = 10, y = -59, z = 0;

        AssertFillCoversBlock(commands, x, y, z, "iron_block", "SW corner");
        AssertFillCoversBlock(commands, x + 14, y, z, "iron_block", "SE corner");
        AssertFillCoversBlock(commands, x, y, z + 14, "iron_block", "NW corner");
        AssertFillCoversBlock(commands, x + 14, y, z + 14, "iron_block", "NE corner");
    }

    [Fact]
    public async Task Grand_Warehouse_DoorFrame()
    {
        var commands = await BuildStructure("redis", "Container", grand: true);
        int x = 10, y = -59, z = 0;

        // Cargo bay: x+5 to x+9, y+1 to y+4, z=0
        AssertDoorCleared(commands, x + 7, y + 1, z, "Cargo bay center bottom");
        AssertDoorCleared(commands, x + 7, y + 4, z, "Cargo bay center top");
    }

    [Fact]
    public async Task Grand_Warehouse_GlowBlock()
    {
        var commands = await BuildStructure("redis", "Container", grand: true);
        // DoorPosition(x+7, y+4, z) → GlowBlock(17, -54, 0)
        AssertSetblock(commands, 17, -54, 0, "glowstone", "Health indicator");
    }

    [Fact]
    public async Task Grand_Warehouse_FloorSpansFootprint()
    {
        var commands = await BuildStructure("redis", "Container", grand: true);
        int x = 10, y = -59, z = 0;
        AssertFillCoversBlock(commands, x + 7, y, z + 7, "iron_block", "Floor center");
    }

    [Fact]
    public async Task Grand_Workshop_CornerBlocks()
    {
        var commands = await BuildStructure("worker", "Executable", grand: true);
        int x = 10, y = -59, z = 0;

        AssertFillCoversBlock(commands, x, y, z, "oak_planks", "SW corner");
        AssertFillCoversBlock(commands, x + 14, y, z, "oak_planks", "SE corner");
        AssertFillCoversBlock(commands, x, y, z + 14, "oak_planks", "NW corner");
        AssertFillCoversBlock(commands, x + 14, y, z + 14, "oak_planks", "NE corner");
    }

    [Fact]
    public async Task Grand_Workshop_DoorFrame()
    {
        var commands = await BuildStructure("worker", "Executable", grand: true);
        int x = 10, y = -59, z = 0;

        // Door: x+6 to x+8, y+1 to y+3, z=0
        AssertDoorCleared(commands, x + 7, y + 1, z, "Door center bottom");
        AssertDoorCleared(commands, x + 7, y + 3, z, "Door center top");
    }

    [Fact]
    public async Task Grand_Workshop_GlowBlock()
    {
        var commands = await BuildStructure("worker", "Executable", grand: true);
        // DoorPosition(x+7, y+4, z) → GlowBlock(17, -54, 0)
        AssertSetblock(commands, 17, -54, 0, "glowstone", "Health indicator");
    }

    [Fact]
    public async Task Grand_Workshop_FloorSpansFootprint()
    {
        var commands = await BuildStructure("worker", "Executable", grand: true);
        int x = 10, y = -59, z = 0;
        AssertFillCoversBlock(commands, x + 7, y, z + 7, "oak_planks", "Floor center");
    }

    [Fact]
    public async Task Grand_Cottage_CornerBlocks()
    {
        var commands = await BuildStructure("misc", "SomeType", grand: true);
        int x = 10, y = -59, z = 0;

        AssertFillCoversBlock(commands, x, y, z, "cobblestone", "SW corner");
        AssertFillCoversBlock(commands, x + 14, y, z, "cobblestone", "SE corner");
        AssertFillCoversBlock(commands, x, y, z + 14, "cobblestone", "NW corner");
        AssertFillCoversBlock(commands, x + 14, y, z + 14, "cobblestone", "NE corner");
    }

    [Fact]
    public async Task Grand_Cottage_DoorFrame()
    {
        var commands = await BuildStructure("misc", "SomeType", grand: true);
        int x = 10, y = -59, z = 0;
        int half = 7; // s/2 where s=14

        // Door: x+half-1 to x+half, y+1 to y+2, z=0
        AssertDoorCleared(commands, x + half, y + 1, z, "Door right bottom");
        AssertDoorCleared(commands, x + half, y + 2, z, "Door right top");
    }

    [Fact]
    public async Task Grand_Cottage_GlowBlock()
    {
        var commands = await BuildStructure("misc", "SomeType", grand: true);
        // DoorPosition(x+half, y+2, z) where half=7 → GlowBlock(17, -56, 0)
        AssertSetblock(commands, 17, -56, 0, "glowstone", "Health indicator");
    }

    [Fact]
    public async Task Grand_Cottage_FloorSpansFootprint()
    {
        var commands = await BuildStructure("misc", "SomeType", grand: true);
        int x = 10, y = -59, z = 0;
        AssertFillCoversBlock(commands, x + 7, y, z + 7, "cobblestone", "Floor center");
    }

    [Fact]
    public async Task Grand_Cylinder_FloorAndDoor()
    {
        var commands = await BuildStructure("db", "postgres", grand: true);
        int x = 10, y = -59, z = 0;

        // Grand cylinder has circular floor with polished_deepslate
        AssertFillCoversBlock(commands, x + 7, y, z + 7, "polished_deepslate", "Floor center");

        // Iron door entrance at x+7, z=0..z+1
        AssertDoorCleared(commands, x + 7, y + 1, z, "Door bottom");
        AssertDoorCleared(commands, x + 7, y + 3, z, "Door top");
    }

    [Fact]
    public async Task Grand_Cylinder_GlowBlock()
    {
        var commands = await BuildStructure("db", "postgres", grand: true);
        // DoorPosition(x+7, y+2, z) → GlowBlock(17, -56, 0)
        AssertSetblock(commands, 17, -56, 0, "glowstone", "Health indicator");
    }

    [Fact]
    public async Task Grand_AzurePavilion_CornerBlocks()
    {
        var commands = await BuildStructure("storage", "azure.storage", grand: true);
        int x = 10, y = -59, z = 0;

        AssertFillCoversBlock(commands, x, y, z, "light_blue_concrete", "SW corner");
        AssertFillCoversBlock(commands, x + 14, y, z, "light_blue_concrete", "SE corner");
        AssertFillCoversBlock(commands, x, y, z + 14, "light_blue_concrete", "NW corner");
        AssertFillCoversBlock(commands, x + 14, y, z + 14, "light_blue_concrete", "NE corner");
    }

    [Fact]
    public async Task Grand_AzurePavilion_DoorFrame()
    {
        var commands = await BuildStructure("storage", "azure.storage", grand: true);
        int x = 10, y = -59, z = 0;
        int half = 7;

        // Door: x+half-1 to x+half, y+1 to y+2, z=0
        AssertDoorCleared(commands, x + half, y + 1, z, "Door right bottom");
        AssertDoorCleared(commands, x + half, y + 2, z, "Door right top");
    }

    [Fact]
    public async Task Grand_AzurePavilion_GlowBlock()
    {
        var commands = await BuildStructure("storage", "azure.storage", grand: true);
        // DoorPosition(x+half, y+3, z) → GlowBlock(17, -55, 0)
        AssertSetblock(commands, 17, -55, 0, "glowstone", "Health indicator");
    }

    [Fact]
    public async Task Grand_AzurePavilion_FloorSpansFootprint()
    {
        var commands = await BuildStructure("storage", "azure.storage", grand: true);
        int x = 10, y = -59, z = 0;
        AssertFillCoversBlock(commands, x + 7, y, z + 7, "light_blue_concrete", "Floor center");
    }

    // ====================================================================
    // SIGN PLACEMENT VERIFICATION
    // ====================================================================

    [Theory]
    [InlineData("api", "Project")]
    [InlineData("redis", "Container")]
    [InlineData("worker", "Executable")]
    [InlineData("misc", "SomeType")]
    [InlineData("db", "postgres")]
    [InlineData("storage", "azure.storage")]
    public async Task Grand_AllTypes_SignContainsResourceName(string name, string type)
    {
        var commands = await BuildStructure(name, type, grand: true);
        var signCmd = commands.FirstOrDefault(c => c.Contains("data merge block") && c.Contains(name));
        Assert.NotNull(signCmd);
    }
}
