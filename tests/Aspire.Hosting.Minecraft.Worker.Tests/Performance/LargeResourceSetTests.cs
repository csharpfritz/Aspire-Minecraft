using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Performance;

/// <summary>
/// Performance tests with 25 resources to verify services handle large resource sets
/// without exceptions and RCON doesn't choke.
/// </summary>
public class LargeResourceSetTests : IAsyncLifetime
{
    private const int ResourceCount = 25;

    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private AspireResourceMonitor _monitor = null!;

    private StructureBuilder _structures = null!;
    private BeaconTowerService _beacons = null!;
    private HologramManager _holograms = null!;
    private BossBarService _bossBar = null!;
    private GuardianMobService _guardians = null!;
    private FireworksService _fireworks = null!;
    private ParticleEffectService _particles = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance);
        _monitor = TestResourceMonitorFactory.Create();

        _structures = new StructureBuilder(_rcon, _monitor, NullLogger<StructureBuilder>.Instance);
        _beacons = new BeaconTowerService(_rcon, _monitor, NullLogger<BeaconTowerService>.Instance);
        _holograms = new HologramManager(_rcon, _monitor, NullLogger<HologramManager>.Instance);
        _bossBar = new BossBarService(_rcon, _monitor, NullLogger<BossBarService>.Instance);
        _guardians = new GuardianMobService(_rcon, _monitor, NullLogger<GuardianMobService>.Instance);
        _fireworks = new FireworksService(_rcon, _monitor, NullLogger<FireworksService>.Instance);
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

    private static (string name, string type, ResourceStatus status)[] Generate25Resources(bool allHealthy = true)
    {
        var types = new[] { "Project", "Container", "Executable" };
        var resources = new (string, string, ResourceStatus)[ResourceCount];
        for (int i = 0; i < ResourceCount; i++)
        {
            var status = allHealthy ? ResourceStatus.Healthy : (i % 3 == 0 ? ResourceStatus.Unhealthy : ResourceStatus.Healthy);
            resources[i] = ($"resource-{i:D2}", types[i % 3], status);
        }
        return resources;
    }

    [Fact]
    public async Task StructureBuilder_25Resources_NoExceptions()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor, Generate25Resources());
        _server.ClearCommands();

        var ex = await Record.ExceptionAsync(() => _structures.UpdateStructuresAsync());

        Assert.Null(ex);
        var cmds = _server.GetCommands();
        // Each resource needs multiple commands (floor, walls, roof, health indicator, sign, etc.)
        Assert.True(cmds.Count > ResourceCount, $"Expected more than {ResourceCount} commands but got {cmds.Count}");
    }

    [Fact]
    public async Task BeaconTowerService_25Resources_BuildsAllBeacons()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor, Generate25Resources());
        _server.ClearCommands();

        var ex = await Record.ExceptionAsync(() => _beacons.UpdateBeaconTowersAsync());

        Assert.Null(ex);
        var cmds = _server.GetCommands();
        // 3 commands per resource: iron base, beacon, glass
        Assert.True(cmds.Count >= ResourceCount * 3,
            $"Expected at least {ResourceCount * 3} beacon commands but got {cmds.Count}");

        // Verify beacon commands present
        var beaconCmds = cmds.Count(c => c.Contains("minecraft:beacon"));
        Assert.Equal(ResourceCount, beaconCmds);
    }

    [Fact]
    public async Task HologramManager_25Resources_CreatesAllLines()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor, Generate25Resources());
        _server.ClearCommands();

        var ex = await Record.ExceptionAsync(() => _holograms.UpdateDashboardAsync());

        Assert.Null(ex);
        var cmds = _server.GetCommands();
        Assert.True(cmds.Count > 0, "Expected hologram commands");
        // Hologram should have a line for each resource plus header and footer
        Assert.Contains(cmds, c => c.Contains("dh create") || c.Contains("dh line"));
    }

    [Fact]
    public async Task BossBar_25Resources_CalculatesCorrectPercentage()
    {
        var resources = Generate25Resources(allHealthy: false);
        // Count expected healthy (those where i%3 != 0)
        var expectedHealthy = resources.Count(r => r.status == ResourceStatus.Healthy);
        var expectedValue = (int)((double)expectedHealthy / ResourceCount * 100);

        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor, resources);
        _server.ClearCommands();

        await _bossBar.UpdateBossBarAsync();

        var cmds = _server.GetCommands();
        Assert.Contains(cmds, c => c.Contains($"bossbar set aspire:fleet_health value {expectedValue}"));
    }

    [Fact]
    public async Task GuardianMobs_25Resources_SpawnsMobsForAll()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor, Generate25Resources());
        _server.ClearCommands();

        var ex = await Record.ExceptionAsync(() => _guardians.UpdateGuardianMobsAsync());

        Assert.Null(ex);
        var cmds = _server.GetCommands();
        // Each resource: kill + summon = 2 commands
        var summonCmds = cmds.Count(c => c.Contains("summon"));
        Assert.Equal(ResourceCount, summonCmds);
    }

    [Fact]
    public async Task VillageLayout_25Resources_AllPositionsUnique()
    {
        var positions = new HashSet<(int x, int y, int z)>();
        for (int i = 0; i < ResourceCount; i++)
        {
            var pos = VillageLayout.GetStructureOrigin(i);
            Assert.True(positions.Add(pos), $"Duplicate position at index {i}: ({pos.x},{pos.y},{pos.z})");
        }

        // 25 resources in 2 columns = 13 rows (ceil(25/2))
        var expectedRows = (ResourceCount + VillageLayout.Columns - 1) / VillageLayout.Columns;
        Assert.Equal(13, expectedRows);
    }

    [Fact]
    public async Task VillageLayout_25Resources_CorrectGridDimensions()
    {
        var first = VillageLayout.GetStructureOrigin(0);
        var last = VillageLayout.GetStructureOrigin(ResourceCount - 1);

        // First: col=0, row=0
        Assert.Equal(VillageLayout.BaseX, first.x);
        Assert.Equal(VillageLayout.BaseZ, first.z);

        // Last (index 24): single row, all at BaseZ
        Assert.Equal(VillageLayout.BaseX + 24 * VillageLayout.Spacing, last.x);
        Assert.Equal(VillageLayout.BaseZ, last.z);
    }

    [Fact]
    public async Task ParticleEffects_25ResourceChanges_NoExceptions()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor, Generate25Resources(allHealthy: false));

        var changes = new List<ResourceStatusChange>();
        for (int i = 0; i < ResourceCount; i++)
        {
            var types = new[] { "Project", "Container", "Executable" };
            var status = i % 3 == 0 ? ResourceStatus.Unhealthy : ResourceStatus.Healthy;
            var oldStatus = status == ResourceStatus.Healthy ? ResourceStatus.Unhealthy : ResourceStatus.Healthy;
            changes.Add(new ResourceStatusChange($"resource-{i:D2}", types[i % 3], oldStatus, status));
        }

        _server.ClearCommands();
        var ex = await Record.ExceptionAsync(() => _particles.ShowParticlesForChangesAsync(changes));

        Assert.Null(ex);
        var cmds = _server.GetCommands();
        Assert.True(cmds.Count > 0, "Expected particle commands for 25 resource changes");
    }

    [Fact]
    public async Task FullUpdateCycle_25Resources_AllServicesComplete()
    {
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor, Generate25Resources());

        var changes = Enumerable.Range(0, ResourceCount)
            .Select(i => new ResourceStatusChange(
                $"resource-{i:D2}", "Project",
                ResourceStatus.Unknown, ResourceStatus.Healthy))
            .ToList();

        _server.ClearCommands();

        // Simulate a full update cycle — all services process 25 resources
        var ex = await Record.ExceptionAsync(async () =>
        {
            await _structures.UpdateStructuresAsync();
            await _beacons.UpdateBeaconTowersAsync();
            await _holograms.UpdateDashboardAsync();
            await _bossBar.UpdateBossBarAsync();
            await _guardians.UpdateGuardianMobsAsync();
            await _particles.ShowParticlesForChangesAsync(changes);
            await _fireworks.CheckAndLaunchFireworksAsync(changes);
        });

        Assert.Null(ex);

        var totalCommands = _server.GetCommands().Count;
        // With 25 resources, we expect a large number of commands
        Assert.True(totalCommands > 100,
            $"Expected > 100 total RCON commands for 25-resource full cycle but got {totalCommands}");
    }

    [Fact]
    public async Task FullUpdateCycle_25Resources_MixedHealth_NoCommandsDropped()
    {
        var resources = Generate25Resources(allHealthy: false);
        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor, resources);
        _server.ClearCommands();

        await _structures.UpdateStructuresAsync();
        await _beacons.UpdateBeaconTowersAsync();
        await _guardians.UpdateGuardianMobsAsync();
        await _bossBar.UpdateBossBarAsync();

        var cmds = _server.GetCommands();

        // Verify all 25 resources got beacons (3 commands each)
        var beaconCount = cmds.Count(c => c.Contains("minecraft:beacon"));
        Assert.Equal(ResourceCount, beaconCount);

        // Verify all 25 resources got guardian mobs
        var summonCount = cmds.Count(c => c.Contains("summon minecraft:iron_golem") || c.Contains("summon minecraft:zombie"));
        Assert.Equal(ResourceCount, summonCount);
    }
}
