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
    private const int ResourceCount = 12;

    [Fact]
    public async Task Paths_Interior_HasCobblestoneAtCenter()
    {
        var (fMinX, fMinZ, fMaxX, fMaxZ) = VillageLayout.GetFencePerimeter(ResourceCount);
        var pathY = VillageLayout.SurfaceY;

        var centerX = (fMinX + fMaxX) / 2;
        var centerZ = (fMinZ + fMaxZ) / 2;

        await RconAssertions.AssertBlockAsync(fixture.Rcon, centerX, pathY, centerZ, "minecraft:cobblestone");
    }

    [Fact]
    public async Task Paths_NearFirstStructure_HasCobblestoneInFront()
    {
        // The path should extend in front of the first structure's entrance
        var (x, _, z) = VillageLayout.GetStructureOrigin(0);
        var pathY = VillageLayout.SurfaceY;

        // One block in front of the structure origin on the Z-min side (south)
        await RconAssertions.AssertBlockAsync(fixture.Rcon, x, pathY, z - 1, "minecraft:cobblestone");
    }
}
