using Aspire.Hosting.Minecraft.Worker.Services;
using Aspire.Hosting.Minecraft.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Services;

/// <summary>
/// Tests for TerrainProbeService — validates binary search logic
/// and graceful fallback when RCON is unresponsive.
/// </summary>
public class TerrainProbeServiceTests : IAsyncLifetime
{
    private MockRconServer _server = null!;
    private RconService _rcon = null!;
    private TerrainProbeService _sut = null!;

    public async Task InitializeAsync()
    {
        _server = new MockRconServer();
        _rcon = new RconService("127.0.0.1", _server.Port, "test",
            NullLogger<RconService>.Instance);
        _sut = new TerrainProbeService(_rcon, NullLogger<TerrainProbeService>.Instance);

        // Wait for RCON to connect
        for (int i = 0; i < 10; i++)
        {
            try { await _rcon.SendCommandAsync("list"); return; }
            catch { await Task.Delay(100); }
        }
    }

    public async Task DisposeAsync()
    {
        // Reset SurfaceY to default after tests
        VillageLayout.SurfaceY = VillageLayout.BaseY;
        await _rcon.DisposeAsync();
        await _server.DisposeAsync();
    }

    [Fact]
    public async Task DetectSurfaceAsync_FallsBackToBaseY_WhenRconReturnsEmpty()
    {
        // MockRconServer returns empty strings — probe treats as unparseable → null
        VillageLayout.SurfaceY = VillageLayout.BaseY;

        await _sut.DetectSurfaceAsync();

        // Should remain at default because probe couldn't parse responses
        Assert.Equal(VillageLayout.BaseY, VillageLayout.SurfaceY);
    }

    [Fact]
    public async Task BinarySearchSurfaceAsync_ReturnsNull_WhenRconReturnsEmpty()
    {
        var result = await _sut.BinarySearchSurfaceAsync(10, 0, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ProbeBlockAsync_ReturnsNull_WhenResponseEmpty()
    {
        var result = await _sut.ProbeBlockAsync(10, 100, 0, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DetectSurfaceAsync_SendsSetblockKeepCommand()
    {
        await _sut.DetectSurfaceAsync();

        var commands = _server.GetCommands();
        // Should have sent at least one setblock ... keep command for probing
        Assert.Contains(commands, c => c.Contains("setblock") && c.Contains("keep"));
    }

    [Fact]
    public void SurfaceY_IsSettableAndReadable()
    {
        var original = VillageLayout.SurfaceY;
        try
        {
            VillageLayout.SurfaceY = 42;
            Assert.Equal(42, VillageLayout.SurfaceY);
        }
        finally
        {
            VillageLayout.SurfaceY = original;
        }
    }
}
