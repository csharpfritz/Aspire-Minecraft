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
    [Fact]
    public async Task Fence_Perimeter_HasOakFenceAtCorners()
    {
        // The sample AppHost monitors 4 resources (api, web, cache, db-host)
        var (minX, minZ, maxX, maxZ) = VillageLayout.GetFencePerimeter(4);
        var fenceY = VillageLayout.SurfaceY + 1;

        // All four corners should have oak_fence posts
        await RconAssertions.AssertBlockAsync(fixture.Rcon, minX, fenceY, minZ, "minecraft:oak_fence");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, maxX, fenceY, minZ, "minecraft:oak_fence");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, minX, fenceY, maxZ, "minecraft:oak_fence");
        await RconAssertions.AssertBlockAsync(fixture.Rcon, maxX, fenceY, maxZ, "minecraft:oak_fence");
    }
}
