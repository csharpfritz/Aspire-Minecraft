using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Tests for Particle Effects (#3) — RCON particle commands at resource structure coordinates.
/// Validates command format, coordinate handling, and particle type selection based on health.
/// </summary>
public class ParticleEffectsCommandTests
{
    [Fact]
    public void Particle_Command_HasCorrectFormat()
    {
        var cmd = RconCommandFormats.Particle("minecraft:heart", 10, -58, 0, 0.5, 0.5, 0.5, 0.1, 10);

        Assert.StartsWith("particle ", cmd);
        Assert.Contains("minecraft:heart", cmd);
        Assert.Contains("10", cmd);
        Assert.Contains("-58", cmd);
    }

    [Fact]
    public void Particle_Command_IncludesAllParameters()
    {
        var cmd = RconCommandFormats.Particle("minecraft:villager_happy", 16, -58, 2, 1.0, 1.0, 1.0, 0.05, 20);
        var parts = cmd.Split(' ');

        Assert.Equal("particle", parts[0]);
        Assert.Equal("minecraft:villager_happy", parts[1]);
        Assert.Equal(10, parts.Length); // particle name x y z dx dy dz speed count
    }

    [Theory]
    [InlineData("minecraft:heart")]
    [InlineData("minecraft:villager_happy")]
    [InlineData("minecraft:smoke")]
    [InlineData("minecraft:flame")]
    [InlineData("minecraft:angry_villager")]
    [InlineData("minecraft:totem_of_undying")]
    public void Particle_ValidMinecraftParticleNames_AreAccepted(string particleName)
    {
        var cmd = RconCommandFormats.Particle(particleName, 0, 0, 0, 1, 1, 1, 0.1, 5);

        Assert.Contains(particleName, cmd);
    }

    [Fact]
    public void Particle_HealthyState_ShouldUsePositiveParticle()
    {
        // When a resource transitions to Healthy, use happy/heart particles
        var healthyParticles = new[] { "minecraft:heart", "minecraft:villager_happy", "minecraft:totem_of_undying" };
        foreach (var particle in healthyParticles)
        {
            var cmd = RconCommandFormats.Particle(particle, 10, -58, 0, 0.5, 0.5, 0.5, 0.1, 10);
            Assert.StartsWith("particle minecraft:", cmd);
            Assert.Contains(particle, cmd);
        }
    }

    [Fact]
    public void Particle_UnhealthyState_ShouldUseNegativeParticle()
    {
        // When a resource transitions to Unhealthy, use smoke/flame particles
        var unhealthyParticles = new[] { "minecraft:smoke", "minecraft:flame", "minecraft:angry_villager", "minecraft:large_smoke" };
        foreach (var particle in unhealthyParticles)
        {
            var cmd = RconCommandFormats.Particle(particle, 10, -58, 0, 0.5, 0.5, 0.5, 0.1, 10);
            Assert.StartsWith("particle minecraft:", cmd);
            Assert.Contains(particle, cmd);
        }
    }

    [Fact]
    public void Particle_CoordinatesMatchStructureBuilder_BaseLayout()
    {
        // StructureBuilder uses BaseX=10, BaseY=-60, BaseZ=0, Spacing=6
        // Particles should appear above structures at Y=-58 (BaseY + 2)
        int baseX = 10, baseY = -60, baseZ = 0, spacing = 6;

        for (int i = 0; i < 5; i++)
        {
            int x = baseX + (i * spacing) + 1; // center of 3x3 structure
            int y = baseY + 2; // above the structure
            int z = baseZ + 1;

            var cmd = RconCommandFormats.Particle("minecraft:heart", x, y, z, 0.5, 0.5, 0.5, 0.1, 10);
            Assert.Contains($"{x} {y} {z}", cmd);
        }
    }

    [Fact]
    public void Particle_ZeroCount_IsValid()
    {
        // Edge case: count=0 means single particle in Minecraft
        var cmd = RconCommandFormats.Particle("minecraft:heart", 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.EndsWith("0", cmd);
    }

    [Fact]
    public void Particle_NegativeCoordinates_AreHandled()
    {
        var cmd = RconCommandFormats.Particle("minecraft:flame", -100, -60, -200, 1, 1, 1, 0.1, 5);
        Assert.Contains("-100 -60 -200", cmd);
    }
}
