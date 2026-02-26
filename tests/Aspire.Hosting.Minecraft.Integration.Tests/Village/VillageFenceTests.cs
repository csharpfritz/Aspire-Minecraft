using Aspire.Hosting.Minecraft.Integration.Tests.Fixtures;
using Aspire.Hosting.Minecraft.Integration.Tests.Helpers;
using Aspire.Hosting.Minecraft.Worker.Services;
using Xunit;

namespace Aspire.Hosting.Minecraft.Integration.Tests.Village;

/// <summary>
/// Verifies the oak fence perimeter is placed around the village at the
/// coordinates calculated by <see cref="VillageLayout.GetFencePerimeter"/>.
/// </summary>
[Collection("Minecraft")]
[Trait("Category", "Integration")]
public class VillageFenceTests(MinecraftAppFixture fixture)
{
    /// <summary>
    /// The GrandVillageDemo AppHost monitors 12 resources total. The fence perimeter
    /// is computed from village bounds + FenceClearance. We use the same count here
    /// so the test stays in sync with the sample app's resource list.
    /// </summary>
    private const int ResourceCount = 12;

    [Fact]
    public async Task Fence_Perimeter_HasOakFenceAtCorners()
    {
        var (minX, minZ, maxX, maxZ) = VillageLayout.GetFencePerimeter(ResourceCount);
        var fenceY = VillageLayout.SurfaceY + 1;

        // All four corners should have oak_fence posts
        await RconAssertions.AssertBlockAsync(fixture.Rcon, minX, fenceY, minZ, "minecraft:oak_fence");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, maxX, fenceY, minZ, "minecraft:oak_fence");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, minX, fenceY, maxZ, "minecraft:oak_fence");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, maxX, fenceY, maxZ, "minecraft:oak_fence");
    }

    [Fact]
    public async Task Fence_Perimeter_HasOakFenceAtEdgeMidpoints()
    {
        var (minX, minZ, maxX, maxZ) = VillageLayout.GetFencePerimeter(ResourceCount);
        var fenceY = VillageLayout.SurfaceY + 1;
        var midX = (minX + maxX) / 2;
        var midZ = (minZ + maxZ) / 2;

        // Midpoint of each fence edge (north, south, east, west)
        await RconAssertions.AssertBlockAsync(fixture.Rcon, midX, fenceY, minZ, "minecraft:oak_fence");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, midX, fenceY, maxZ, "minecraft:oak_fence");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, minX, fenceY, midZ, "minecraft:oak_fence");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, maxX, fenceY, midZ, "minecraft:oak_fence");
    }
}
