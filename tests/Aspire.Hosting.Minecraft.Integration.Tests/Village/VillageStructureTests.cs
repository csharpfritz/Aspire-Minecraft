using Aspire.Hosting.Minecraft.Integration.Tests.Fixtures;
using Aspire.Hosting.Minecraft.Integration.Tests.Helpers;
using Aspire.Hosting.Minecraft.Worker.Services;
using Xunit;

namespace Aspire.Hosting.Minecraft.Integration.Tests.Village;

/// <summary>
/// Verifies that the first structure (watchtower at index 0) is built at the
/// correct origin coordinates. Project-type resources get watchtowers with
/// cobblestone outer walls.
/// </summary>
[Collection("Minecraft")]
public class VillageStructureTests(MinecraftAppFixture fixture)
{
    [Fact]
    public async Task Structure_Index0_WatchtowerHasCobblestoneAtOrigin()
    {
        // Watchtower at index 0: origin is (10, SurfaceY+1, 0)
        var (x, y, z) = VillageLayout.GetStructureOrigin(0);

        // Outer shell corners should be cobblestone (7×7 footprint)
        await RconAssertions.AssertBlockAsync(fixture.Rcon, x, y, z, "minecraft:cobblestone");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, x + 6, y, z, "minecraft:cobblestone");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, x, y, z + 6, "minecraft:cobblestone");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, x + 6, y, z + 6, "minecraft:cobblestone");
    }
}
