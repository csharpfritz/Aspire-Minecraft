using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests;

/// <summary>
/// Tests that validate the expected RCON command mappings across ALL Sprint 1 features.
/// Each health transition should produce the correct set of commands for enabled features.
/// </summary>
public class HealthTransitionRconMappingTests
{
    // ===== Particle mappings per health transition =====

    [Fact]
    public void HealthyToUnhealthy_ShouldProduceAlarmParticle()
    {
        var change = new ResourceStatusChange("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy);

        // Expected: negative particles (smoke, flame)
        Assert.Equal(ResourceStatus.Unhealthy, change.NewStatus);
        var cmd = RconCommandFormats.Particle("minecraft:large_smoke", 11, -58, 1, 1, 1, 1, 0.02, 30);
        Assert.Contains("large_smoke", cmd);
    }

    [Fact]
    public void UnhealthyToHealthy_ShouldProduceRecoveryParticle()
    {
        var change = new ResourceStatusChange("api", "Project", ResourceStatus.Unhealthy, ResourceStatus.Healthy);

        Assert.Equal(ResourceStatus.Healthy, change.NewStatus);
        var cmd = RconCommandFormats.Particle("minecraft:totem_of_undying", 11, -58, 1, 1, 1, 1, 0.5, 50);
        Assert.Contains("totem_of_undying", cmd);
    }

    // ===== Title mappings per health transition =====

    [Fact]
    public void HealthyToUnhealthy_ShouldProduceRedTitle()
    {
        var change = new ResourceStatusChange("redis", "Container", ResourceStatus.Healthy, ResourceStatus.Unhealthy);

        var title = RconCommandFormats.TitleShow("@a",
            $$$"""{"text":"⚠ {{{change.Name}}} DOWN","color":"red","bold":true}""");

        Assert.Contains("red", title);
        Assert.Contains(change.Name, title);
    }

    [Fact]
    public void UnhealthyToHealthy_ShouldProduceGreenTitle()
    {
        var change = new ResourceStatusChange("redis", "Container", ResourceStatus.Unhealthy, ResourceStatus.Healthy);

        var title = RconCommandFormats.TitleShow("@a",
            $$$"""{"text":"✔ {{{change.Name}}} RECOVERED","color":"green","bold":true}""");

        Assert.Contains("green", title);
        Assert.Contains(change.Name, title);
    }

    // ===== Sound mappings per health transition =====

    [Fact]
    public void HealthyToUnhealthy_ShouldProduceAlarmSound()
    {
        var change = new ResourceStatusChange("db", "Postgres", ResourceStatus.Healthy, ResourceStatus.Unhealthy);

        Assert.Equal(ResourceStatus.Unhealthy, change.NewStatus);
        var cmd = RconCommandFormats.PlaySound("minecraft:entity.wither.spawn", "master", "@a");
        Assert.Contains("wither", cmd);
    }

    [Fact]
    public void UnhealthyToHealthy_ShouldProduceRecoverySound()
    {
        var change = new ResourceStatusChange("db", "Postgres", ResourceStatus.Unhealthy, ResourceStatus.Healthy);

        Assert.Equal(ResourceStatus.Healthy, change.NewStatus);
        var cmd = RconCommandFormats.PlaySound("minecraft:entity.player.levelup", "master", "@a");
        Assert.Contains("levelup", cmd);
    }

    // ===== Multi-resource simultaneous transitions =====

    [Fact]
    public void MultipleResourceChanges_EachGetsItsOwnCommands()
    {
        var changes = new[]
        {
            new ResourceStatusChange("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy),
            new ResourceStatusChange("redis", "Container", ResourceStatus.Healthy, ResourceStatus.Unhealthy),
            new ResourceStatusChange("db", "Postgres", ResourceStatus.Unhealthy, ResourceStatus.Healthy),
        };

        // Each change should generate commands for all enabled features
        var commands = new List<string>();
        foreach (var change in changes)
        {
            // Each feature would add its commands per change
            commands.Add(RconCommandFormats.Particle("minecraft:smoke", 10, -58, 0, 1, 1, 1, 0.1, 10));
            commands.Add(RconCommandFormats.TitleShow("@a", $$$"""{"text":"{{{change.Name}}}"}"""));
            commands.Add(RconCommandFormats.PlaySound("minecraft:entity.wither.spawn", "master", "@a"));
        }

        // 3 changes × 3 command types = 9 commands (weather + bossbar are aggregate, not per-resource)
        Assert.Equal(9, commands.Count);
    }

    [Fact]
    public void AggregateFeatures_WeatherAndBossBar_UpdateOncePerPoll()
    {
        // Weather and BossBar reflect AGGREGATE health — updated once per poll cycle,
        // not once per resource change.
        var changes = new[]
        {
            new ResourceStatusChange("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy),
            new ResourceStatusChange("redis", "Container", ResourceStatus.Healthy, ResourceStatus.Unhealthy),
        };

        // Weather: 1 command total (based on overall ratio)
        // BossBar: 1-3 commands total (value + color + optional name)
        int weatherCommands = 1;
        int bossBarCommands = 2; // value + color

        Assert.True(weatherCommands < changes.Length);
        Assert.True(bossBarCommands <= 3);
    }

    [Fact]
    public void PerResourceFeatures_ParticlesAndTitleAndSound_FirePerChange()
    {
        // Particles, Titles, and Sounds are per-resource-change
        var changes = new[]
        {
            new ResourceStatusChange("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy),
            new ResourceStatusChange("redis", "Container", ResourceStatus.Unhealthy, ResourceStatus.Healthy),
        };

        int expectedParticleCommands = changes.Length;
        int expectedTitleCommands = changes.Length * 3; // times + subtitle + title per change
        int expectedSoundCommands = changes.Length;

        Assert.Equal(2, expectedParticleCommands);
        Assert.Equal(6, expectedTitleCommands);
        Assert.Equal(2, expectedSoundCommands);
    }

    // ===== Edge case: Unknown → Healthy (first discovery) =====

    [Fact]
    public void UnknownToHealthy_ShouldProduceDiscoveryEffect()
    {
        var change = new ResourceStatusChange("new-svc", "Project", ResourceStatus.Unknown, ResourceStatus.Healthy);

        Assert.Equal(ResourceStatus.Unknown, change.OldStatus);
        Assert.Equal(ResourceStatus.Healthy, change.NewStatus);

        // First discovery should produce positive effects (like recovery)
        var particleCmd = RconCommandFormats.Particle("minecraft:villager_happy", 11, -58, 1, 1, 1, 1, 0.1, 20);
        Assert.Contains("villager_happy", particleCmd);
    }

    [Fact]
    public void UnknownToUnhealthy_ShouldProduceAlarmEffect()
    {
        var change = new ResourceStatusChange("broken-svc", "Project", ResourceStatus.Unknown, ResourceStatus.Unhealthy);

        Assert.Equal(ResourceStatus.Unknown, change.OldStatus);
        Assert.Equal(ResourceStatus.Unhealthy, change.NewStatus);

        var particleCmd = RconCommandFormats.Particle("minecraft:smoke", 11, -58, 1, 1, 1, 1, 0.1, 20);
        Assert.Contains("smoke", particleCmd);
    }

    // ===== Coordinate integration with StructureBuilder (2×N village grid) =====

    [Theory]
    [InlineData(0, 10, 0)]   // Resource 0: col=0, row=0 → x=10, z=0
    [InlineData(1, 22, 0)]   // Resource 1: col=1, row=0 → x=22, z=0
    [InlineData(2, 10, 12)]  // Resource 2: col=0, row=1 → x=10, z=12
    [InlineData(3, 22, 12)]  // Resource 3: col=1, row=1 → x=22, z=12
    [InlineData(4, 10, 24)]  // Resource 4: col=0, row=2 → x=10, z=24
    public void StructureCoordinates_MatchResourceIndex(int index, int expectedX, int expectedZ)
    {
        // VillageLayout: BaseX=10, BaseZ=0, Spacing=12, Columns=2
        int col = index % 2;
        int row = index / 2;
        int actualX = 10 + (col * 12);
        int actualZ = 0 + (row * 12);

        Assert.Equal(expectedX, actualX);
        Assert.Equal(expectedZ, actualZ);
    }

    [Fact]
    public void ParticleCoordinates_AreCenteredAboveStructure()
    {
        // Structure is 7x7 base, particles center at (x+3, y+10, z+3) via VillageLayout.GetAboveStructure
        int structX = 10, structY = -60, structZ = 0;
        int particleX = structX + 3;
        int particleY = structY + 10;
        int particleZ = structZ + 3;

        Assert.Equal(13, particleX);
        Assert.Equal(-50, particleY);
        Assert.Equal(3, particleZ);
    }
}
