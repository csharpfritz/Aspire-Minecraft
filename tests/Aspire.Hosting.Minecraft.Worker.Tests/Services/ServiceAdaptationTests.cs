using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Services;

/// <summary>
/// Tests that adapted services use VillageLayout properties correctly
/// instead of hardcoded coordinates.
/// </summary>
public class ServiceAdaptationTests : IAsyncLifetime
{
    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private AspireResourceMonitor _monitor = null!;

    public async Task InitializeAsync()
    {
        VillageLayout.ResetLayout();
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance, maxCommandsPerSecond: 1000);
        _monitor = TestResourceMonitorFactory.Create();

        await WaitForRconConnected();
    }

    public async Task DisposeAsync()
    {
        await _rcon.DisposeAsync();
        await _server.DisposeAsync();
        VillageLayout.ResetLayout();
    }

    private async Task WaitForRconConnected()
    {
        for (int i = 0; i < 10; i++)
        {
            try { await _rcon.SendCommandAsync("list"); return; }
            catch { await Task.Delay(100); }
        }
    }

    /// <summary>
    /// Verify guardian spawn coordinates match VillageLayout positions.
    /// Guardian is placed at (origin.x + StructureSize/2, SurfaceY+2, origin.z - 3).
    /// </summary>
    [Fact]
    public async Task GuardianMobService_UsesStructureOrigin_NotHardcoded()
    {
        var guardianService = new GuardianMobService(_rcon, _monitor,
            NullLogger<GuardianMobService>.Instance);

        TestResourceMonitorFactory.SetResourcesWithTypes(_monitor,
            ("test-app", "Project", ResourceStatus.Healthy)
        );
        _server.ClearCommands();

        await guardianService.UpdateGuardianMobsAsync();

        var commands = _server.GetCommands();

        // Calculate expected position from VillageLayout
        var (ox, _, oz) = VillageLayout.GetStructureOrigin(0);
        var expectedX = ox + (VillageLayout.StructureSize / 2);
        var expectedY = VillageLayout.SurfaceY + 2;
        var expectedZ = oz - 3;

        // Verify summon command uses VillageLayout-derived coordinates
        var summonCmd = commands.FirstOrDefault(c => c.Contains("summon minecraft:iron_golem"));
        Assert.NotNull(summonCmd);
        Assert.Contains($"{expectedX} {expectedY} {expectedZ}", summonCmd);
    }

    /// <summary>
    /// Verify redstone wire uses StructureSize/2 for entrance offset.
    /// The wire start/end positions use origin.x + half at z - 1.
    /// </summary>
    [Fact]
    public async Task RedstoneDependencyService_UsesStructureSizeHalf_ForEntrance()
    {
        var redstoneService = new RedstoneDependencyService(_rcon, _monitor,
            NullLogger<RedstoneDependencyService>.Instance);

        TestResourceMonitorFactory.SetResourcesWithDependencies(_monitor,
            ("parent", "Project", ResourceStatus.Healthy, []),
            ("child", "Container", ResourceStatus.Healthy, ["parent"])
        );
        _server.ClearCommands();

        await redstoneService.InitializeAsync();

        var commands = _server.GetCommands();

        // Calculate expected entrance positions from VillageLayout
        var half = VillageLayout.StructureSize / 2;
        var (px, _, pz) = VillageLayout.GetStructureOrigin(0);
        var parentEntranceX = px + half;
        var parentEntranceZ = pz - 1;

        // Verify redstone_wire commands use the VillageLayout-derived entrance offset
        var wireCommands = commands.Where(c => c.Contains("minecraft:redstone_wire")).ToList();
        Assert.NotEmpty(wireCommands);

        // The first wire position should be at the parent entrance
        Assert.Contains(wireCommands, c => c.Contains($"{parentEntranceX}") && c.Contains($"{parentEntranceZ}"));
    }
}
