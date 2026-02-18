using Aspire.Hosting.Minecraft.Integration.Tests.Fixtures;
using Aspire.Hosting.Minecraft.Integration.Tests.Helpers;
using Aspire.Hosting.Minecraft.Worker.Services;
using Xunit;

namespace Aspire.Hosting.Minecraft.Integration.Tests.Village;

/// <summary>
/// Verifies that the first structure (grand watchtower at index 0) is built at the
/// correct origin coordinates. Project-type resources get grand watchtowers (15×15)
/// with a mossy stone brick base and stone brick walls.
/// </summary>
[Collection("Minecraft")]
[Trait("Category", "Integration")]
public class VillageStructureTests(MinecraftAppFixture fixture)
{
    [Fact]
    public async Task Structure_Index0_WatchtowerHasMossyStoneBrickBase()
    {
        // Grand Watchtower at index 0: origin is (10, SurfaceY+1, 0)
        // Base is a 15×15 mossy_stone_bricks plinth at y level
        var (x, y, z) = VillageLayout.GetStructureOrigin(0);
        var s = VillageLayout.StructureSize - 1; // 14

        await RconAssertions.AssertBlockAsync(fixture.Rcon, x, y, z, "minecraft:mossy_stone_bricks");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, x + s, y, z, "minecraft:mossy_stone_bricks");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, x, y, z + s, "minecraft:mossy_stone_bricks");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, x + s, y, z + s, "minecraft:mossy_stone_bricks");
    }

    [Fact]
    public async Task Structure_Index0_WatchtowerHasStoneBrickWalls()
    {
        // Grand Watchtower walls start at y+1 and are stone_bricks
        var (x, y, z) = VillageLayout.GetStructureOrigin(0);
        var half = VillageLayout.StructureSize / 2; // 7

        // Check the front wall at y+3 (above door height) at center
        await RconAssertions.AssertBlockAsync(fixture.Rcon, x + half, y + 5, z, "minecraft:stone_bricks");
    }

    /// <summary>
    /// Demonstrates dual verification: RCON (live server query) and MCA (Anvil file read)
    /// checking the same block. RCON is always available; MCA requires a host-mounted world
    /// save directory. When WorldSaveDirectory is null, the MCA portion is gracefully skipped.
    /// </summary>
    [Fact]
    public async Task Structure_Index0_DualVerification_RconAndMca()
    {
        var (x, y, z) = VillageLayout.GetStructureOrigin(0);

        // Method 1: RCON — always works, queries the live server
        await RconAssertions.AssertBlockAsync(fixture.Rcon, x, y, z, "minecraft:mossy_stone_bricks");

        // Method 2: MCA — reads the Anvil region file directly.
        // Sends "save-all flush" first, then opens the .mca file.
        // Gracefully skips if WorldSaveDirectory is null (no bind mount configured).
        await AnvilTestHelper.VerifyBlockAsync(fixture, x, y, z, "mossy_stone_bricks");
    }
}
