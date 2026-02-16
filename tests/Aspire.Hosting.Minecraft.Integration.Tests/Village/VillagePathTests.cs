using Aspire.Hosting.Minecraft.Integration.Tests.Fixtures;
using Aspire.Hosting.Minecraft.Integration.Tests.Helpers;
using Aspire.Hosting.Minecraft.Worker.Services;
using Xunit;

namespace Aspire.Hosting.Minecraft.Integration.Tests.Village;

/// <summary>
/// Verifies that cobblestone paths are placed between structures in the village.
/// Paths are laid at <see cref="VillageLayout.SurfaceY"/> between the fence perimeter edges.
/// </summary>
[Collection("Minecraft")]
[Trait("Category", "Integration")]
public class VillagePathTests(MinecraftAppFixture fixture)
{
    [Fact]
    public async Task Paths_Interior_HasCobblestoneAtCenter()
    {
        // Paths fill the interior area at the surface level
        var (fMinX, fMinZ, fMaxX, fMaxZ) = VillageLayout.GetFencePerimeter(4);
        var pathY = VillageLayout.SurfaceY;

        // Center of the path area should be cobblestone
        var centerX = (fMinX + fMaxX) / 2;
        var centerZ = (fMinZ + fMaxZ) / 2;

        await RconAssertions.AssertBlockAsync(fixture.Rcon, centerX, pathY, centerZ, "minecraft:cobblestone");
    }
}
