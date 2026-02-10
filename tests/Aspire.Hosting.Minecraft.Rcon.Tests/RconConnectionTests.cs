using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.Minecraft.Rcon.Tests;

public class RconConnectionTests
{
    [Fact]
    public void IsConnected_WhenNew_ReturnsFalse()
    {
        var connection = new RconConnection("localhost", 25575, "password", NullLogger.Instance);
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public async Task DisposeAsync_WhenNotConnected_DoesNotThrow()
    {
        var connection = new RconConnection("localhost", 25575, "password", NullLogger.Instance);
        // Should complete without throwing
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task SendCommandAsync_UnreachableHost_ThrowsAfterCancellation()
    {
        var connection = new RconConnection("127.0.0.1", 1, "password", NullLogger.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connection.SendCommandAsync("list", cts.Token));
    }
}
