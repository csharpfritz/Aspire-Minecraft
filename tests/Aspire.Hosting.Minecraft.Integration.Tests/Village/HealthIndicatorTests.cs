using Aspire.Hosting.Minecraft.Integration.Tests.Fixtures;
using Aspire.Hosting.Minecraft.Integration.Tests.Helpers;
using Aspire.Hosting.Minecraft.Worker.Services;
using Xunit;

namespace Aspire.Hosting.Minecraft.Integration.Tests.Village;

/// <summary>
/// Verifies that health indicator glow blocks are placed above structure doors.
/// Healthy resources get glowstone, unhealthy get redstone_lamp, unknown get sea_lantern.
/// </summary>
[Collection("Minecraft")]
[Trait("Category", "Integration")]
public class HealthIndicatorTests(MinecraftAppFixture fixture)
{
    [Fact]
    public async Task HealthIndicator_FirstResource_HasGlowBlock()
    {
        var (x, y, z) = VillageLayout.GetStructureOrigin(0);

        // Grand Watchtower DoorPosition: (x+7, y+4, z) → GlowBlock at (x+7, y+5, z)
        var half = VillageLayout.StructureSize / 2; // 7
        var glowX = x + half;
        var glowY = y + 5; // DoorPosition.TopY (y+4) + 1
        var glowZ = z;

        // The glow block should be glowstone (healthy), redstone_lamp (unhealthy),
        // or sea_lantern (unknown) depending on resource state at test time.
        // Try all three — at least one should match.
        var glowResult = await fixture.Rcon.SendCommandAsync(
            $"execute if block {glowX} {glowY} {glowZ} minecraft:glowstone");
        var lampResult = await fixture.Rcon.SendCommandAsync(
            $"execute if block {glowX} {glowY} {glowZ} minecraft:redstone_lamp");
        var lanternResult = await fixture.Rcon.SendCommandAsync(
            $"execute if block {glowX} {glowY} {glowZ} minecraft:sea_lantern");

        var foundIndicator = string.IsNullOrEmpty(glowResult)
            || string.IsNullOrEmpty(lampResult)
            || string.IsNullOrEmpty(lanternResult);

        Assert.True(foundIndicator,
            $"Expected a health indicator (glowstone/redstone_lamp/sea_lantern) at ({glowX}, {glowY}, {glowZ})");
    }
}
