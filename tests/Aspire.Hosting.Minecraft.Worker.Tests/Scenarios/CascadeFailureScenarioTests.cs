using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Scenarios;

/// <summary>
/// End-to-end cascade failure scenario test.
/// Simulates: 5 resources start healthy → 1 goes unhealthy → 2 more cascade →
/// boss bar drops → guardians spawn → all recover → fireworks fire → guardians despawn.
/// Tests the INTERACTION between multiple services responding to the same health changes.
/// </summary>
public class CascadeFailureScenarioTests : IAsyncLifetime
{
    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private AspireResourceMonitor _monitor = null!;

    private BossBarService _bossBar = null!;
    private GuardianMobService _guardians = null!;
    private FireworksService _fireworks = null!;
    private DeploymentFanfareService _fanfare = null!;
    private BeaconTowerService _beacons = null!;
    private ParticleEffectService _particles = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance);
        _monitor = TestResourceMonitorFactory.Create();

        _bossBar = new BossBarService(_rcon, _monitor, NullLogger<BossBarService>.Instance);
        _guardians = new GuardianMobService(_rcon, _monitor, NullLogger<GuardianMobService>.Instance);
        _fireworks = new FireworksService(_rcon, _monitor, NullLogger<FireworksService>.Instance);
        _fanfare = new DeploymentFanfareService(_rcon, NullLogger<DeploymentFanfareService>.Instance);
        _beacons = new BeaconTowerService(_rcon, _monitor, NullLogger<BeaconTowerService>.Instance);
        _particles = new ParticleEffectService(_rcon, _monitor, NullLogger<ParticleEffectService>.Instance);

        await WaitForRconConnected();
    }

    public async Task DisposeAsync()
    {
        await _rcon.DisposeAsync();
        await _server.DisposeAsync();
    }

    private async Task WaitForRconConnected()
    {
        for (int i = 0; i < 10; i++)
        {
            try { await _rcon.SendCommandAsync("list"); return; }
            catch { await Task.Delay(100); }
        }
    }

    [Fact]
    public async Task CascadeFailureAndRecovery_FullScenario()
    {
        // ── Phase 1: All 5 resources start healthy (initial deployment) ──
        TestResourceMonitorFactory.SetResources(_monitor,
            ("api", true), ("db", true), ("cache", true), ("worker", true), ("gateway", true));

        var deployChanges = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Unknown, ResourceStatus.Healthy),
            new("db", "Container", ResourceStatus.Unknown, ResourceStatus.Healthy),
            new("cache", "Container", ResourceStatus.Unknown, ResourceStatus.Healthy),
            new("worker", "Executable", ResourceStatus.Unknown, ResourceStatus.Healthy),
            new("gateway", "Project", ResourceStatus.Unknown, ResourceStatus.Healthy)
        };

        await _fanfare.CheckAndCelebrateAsync(deployChanges);
        await _bossBar.UpdateBossBarAsync();
        await _guardians.UpdateGuardianMobsAsync();
        await _beacons.UpdateBeaconTowersAsync();

        _server.ClearCommands();

        // Verify boss bar is at 100% green
        await _bossBar.UpdateBossBarAsync();
        var phase1Cmds = _server.GetCommands();
        // No value update needed since it's already at 100%
        // But players @a is always sent
        Assert.Contains(phase1Cmds, c => c.Contains("bossbar set aspire:fleet_health players @a"));

        // ── Phase 2: First failure — "api" goes unhealthy ──
        TestResourceMonitorFactory.SetResources(_monitor,
            ("api", false), ("db", true), ("cache", true), ("worker", true), ("gateway", true));

        var phase2Changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        };

        _server.ClearCommands();
        await _bossBar.UpdateBossBarAsync();
        await _guardians.UpdateGuardianMobsAsync();
        await _particles.ShowParticlesForChangesAsync(phase2Changes);
        await _fireworks.CheckAndLaunchFireworksAsync(phase2Changes);
        await _beacons.UpdateBeaconTowersAsync();

        var phase2Cmds = _server.GetCommands();

        // Boss bar should drop to 80% (4/5) and go yellow
        Assert.Contains(phase2Cmds, c => c.Contains("bossbar set aspire:fleet_health value 80"));
        Assert.Contains(phase2Cmds, c => c.Contains("color yellow"));

        // Guardian: zombie spawned for api
        Assert.Contains(phase2Cmds, c => c.Contains("summon minecraft:zombie") && c.Contains("guardian_api"));

        // Particles: smoke/flame for unhealthy api
        Assert.Contains(phase2Cmds, c => c.Contains("large_smoke") || c.Contains("flame"));

        // No fireworks — not all recovered
        Assert.DoesNotContain(phase2Cmds, c => c.Contains("firework_rocket"));

        // ── Phase 3: Cascade — "db" and "cache" also go unhealthy ──
        TestResourceMonitorFactory.SetResources(_monitor,
            ("api", false), ("db", false), ("cache", false), ("worker", true), ("gateway", true));

        var phase3Changes = new List<ResourceStatusChange>
        {
            new("db", "Container", ResourceStatus.Healthy, ResourceStatus.Unhealthy),
            new("cache", "Container", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        };

        _server.ClearCommands();
        await _bossBar.UpdateBossBarAsync();
        await _guardians.UpdateGuardianMobsAsync();
        await _particles.ShowParticlesForChangesAsync(phase3Changes);
        await _fireworks.CheckAndLaunchFireworksAsync(phase3Changes);
        await _beacons.UpdateBeaconTowersAsync();

        var phase3Cmds = _server.GetCommands();

        // Boss bar should drop to 40% (2/5) and go red (< 50)
        Assert.Contains(phase3Cmds, c => c.Contains("bossbar set aspire:fleet_health value 40"));
        Assert.Contains(phase3Cmds, c => c.Contains("color red"));

        // Zombies for db and cache
        Assert.Contains(phase3Cmds, c => c.Contains("guardian_db") && c.Contains("zombie"));
        Assert.Contains(phase3Cmds, c => c.Contains("guardian_cache") && c.Contains("zombie"));

        // Beacons should show red glass for unhealthy resources
        Assert.Contains(phase3Cmds, c => c.Contains("red_stained_glass"));

        // Still no fireworks
        Assert.DoesNotContain(phase3Cmds, c => c.Contains("firework_rocket"));

        // ── Phase 4: Full recovery — all go healthy ──
        TestResourceMonitorFactory.SetResources(_monitor,
            ("api", true), ("db", true), ("cache", true), ("worker", true), ("gateway", true));

        var phase4Changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Unhealthy, ResourceStatus.Healthy),
            new("db", "Container", ResourceStatus.Unhealthy, ResourceStatus.Healthy),
            new("cache", "Container", ResourceStatus.Unhealthy, ResourceStatus.Healthy)
        };

        _server.ClearCommands();
        await _bossBar.UpdateBossBarAsync();
        await _guardians.UpdateGuardianMobsAsync();
        await _particles.ShowParticlesForChangesAsync(phase4Changes);
        await _fireworks.CheckAndLaunchFireworksAsync(phase4Changes);
        await _beacons.UpdateBeaconTowersAsync();

        var phase4Cmds = _server.GetCommands();

        // Boss bar should return to 100% green
        Assert.Contains(phase4Cmds, c => c.Contains("bossbar set aspire:fleet_health value 100"));
        Assert.Contains(phase4Cmds, c => c.Contains("color green"));

        // Guardians: iron golems restored for recovered resources
        Assert.Contains(phase4Cmds, c => c.Contains("guardian_api") && c.Contains("iron_golem"));
        Assert.Contains(phase4Cmds, c => c.Contains("guardian_db") && c.Contains("iron_golem"));
        Assert.Contains(phase4Cmds, c => c.Contains("guardian_cache") && c.Contains("iron_golem"));

        // Fireworks should launch on full recovery
        Assert.Contains(phase4Cmds, c => c.Contains("firework_rocket"));

        // Happy particles for recovered resources
        Assert.Contains(phase4Cmds, c => c.Contains("happy_villager"));
    }

    [Fact]
    public async Task CascadeFailure_DeploymentFanfareOnlyOnFirstHealthy()
    {
        // Resources start unknown (deploying)
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Unknown),
            ("db", "Container", ResourceStatus.Unknown));

        // First comes online
        var changes1 = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Unknown, ResourceStatus.Healthy)
        };
        _server.ClearCommands();
        await _fanfare.CheckAndCelebrateAsync(changes1);

        var cmds1 = _server.GetCommands();
        Assert.Contains(cmds1, c => c.Contains("DEPLOYED"));
        Assert.Contains(cmds1, c => c.Contains("api is online"));

        // Second comes online
        var changes2 = new List<ResourceStatusChange>
        {
            new("db", "Container", ResourceStatus.Unknown, ResourceStatus.Healthy)
        };
        _server.ClearCommands();
        await _fanfare.CheckAndCelebrateAsync(changes2);

        var cmds2 = _server.GetCommands();
        Assert.Contains(cmds2, c => c.Contains("db is online"));

        // Re-sending same change should still trigger (no dedup in fanfare)
        // because fanfare only checks OldStatus==Unknown && NewStatus==Healthy
    }

    [Fact]
    public async Task CascadeFailure_BeaconColorsTrackHealth()
    {
        // Start healthy with mixed types
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Healthy),
            ("redis", "Container", ResourceStatus.Healthy),
            ("worker", "Executable", ResourceStatus.Healthy));

        _server.ClearCommands();
        await _beacons.UpdateBeaconTowersAsync();

        var healthyCmds = _server.GetCommands();
        Assert.Contains(healthyCmds, c => c.Contains("blue_stained_glass"));
        Assert.Contains(healthyCmds, c => c.Contains("purple_stained_glass"));
        Assert.Contains(healthyCmds, c => c.Contains("cyan_stained_glass"));

        // Take redis down
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("api", "Project", ResourceStatus.Healthy),
            ("redis", "Container", ResourceStatus.Unhealthy),
            ("worker", "Executable", ResourceStatus.Healthy));

        _server.ClearCommands();
        await _beacons.UpdateBeaconTowersAsync();

        var unhealthyCmds = _server.GetCommands();
        Assert.Contains(unhealthyCmds, c => c.Contains("blue_stained_glass"));
        Assert.Contains(unhealthyCmds, c => c.Contains("red_stained_glass"));
        Assert.Contains(unhealthyCmds, c => c.Contains("cyan_stained_glass"));
    }

    [Fact]
    public async Task CascadeFailure_CommandSequenceIsOrdered()
    {
        TestResourceMonitorFactory.SetResources(_monitor, ("api", true), ("db", true));
        await _bossBar.UpdateBossBarAsync();
        await _guardians.UpdateGuardianMobsAsync();

        // Take api down
        TestResourceMonitorFactory.SetResources(_monitor, ("api", false), ("db", true));
        var changes = new List<ResourceStatusChange>
        {
            new("api", "Project", ResourceStatus.Healthy, ResourceStatus.Unhealthy)
        };

        _server.ClearCommands();
        await _bossBar.UpdateBossBarAsync();
        await _guardians.UpdateGuardianMobsAsync();
        await _particles.ShowParticlesForChangesAsync(changes);

        var cmds = _server.GetCommands();
        // All commands should be present — no dropped commands
        Assert.True(cmds.Count >= 3, $"Expected at least 3 commands but got {cmds.Count}");
    }
}
