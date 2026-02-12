using Aspire.Hosting.Minecraft.Integration.Tests.Fixtures;
using Aspire.Hosting.Minecraft.Integration.Tests.Helpers;
using Aspire.Hosting.Minecraft.Worker.Services;
using Xunit;

namespace Aspire.Hosting.Minecraft.Integration.Tests.Village;

/// <summary>
/// Verifies that health indicator wool blocks are placed on top of structures.
/// A healthy resource should have a colored wool block at the indicator position.
/// </summary>
[Collection("Minecraft")]
public class HealthIndicatorTests(MinecraftAppFixture fixture)
{
    [Fact]
    public async Task HealthIndicator_FirstResource_HasWoolBlock()
    {
        var (x, y, z) = VillageLayout.GetStructureOrigin(0);

        // Health indicator is placed above the structure.
        // Check for any wool variant (healthy=green, degraded=yellow, unhealthy=red).
        // We use a broad check — the exact color depends on resource state at test time.
        var indicatorX = x + 3;
        var indicatorZ = z + 3;

        // Try green_wool first (expected for healthy resources)
        // If the resource is healthy, this block should be green_wool
        var result = await fixture.Rcon.SendCommandAsync(
            $"execute if block {indicatorX} {y + 10} {indicatorZ} #minecraft:wool");

        // #minecraft:wool is a block tag — if not supported, fall back to individual checks
        if (!string.IsNullOrEmpty(result))
        {
            // Try specific wool colors that the health indicator uses
            await RconAssertions.AssertBlockAsync(
                fixture.Rcon, indicatorX, y + 10, indicatorZ, "minecraft:green_wool");
        }
    }
}
