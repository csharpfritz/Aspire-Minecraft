using System.Diagnostics;
using Aspire.Hosting.Minecraft.Rcon;
using Aspire.Hosting.Testing;
using Xunit;

namespace Aspire.Hosting.Minecraft.Integration.Tests.Fixtures;

/// <summary>
/// Shared fixture that starts the full Aspire AppHost (Minecraft server + worker) once per test collection.
/// Exposes an authenticated <see cref="RconClient"/> and the BlueMap URL for test assertions.
/// Uses poll-based readiness instead of fixed delays.
/// </summary>
public sealed class MinecraftAppFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    /// <summary>The running Aspire distributed application.</summary>
    public DistributedApplication App => _app ?? throw new InvalidOperationException("Fixture not initialized.");

    /// <summary>Authenticated RCON client connected to the Minecraft server.</summary>
    public RconClient Rcon { get; } = new();

    /// <summary>Base URL for the BlueMap web UI (e.g., "http://localhost:8100").</summary>
    public string BlueMapUrl { get; private set; } = string.Empty;

    /// <summary>Whether the fixture successfully initialized and the village is built.</summary>
    public bool IsReady { get; private set; }

    public async Task InitializeAsync()
    {
        // 1. Build the Aspire AppHost using the testing builder
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.MinecraftAspireDemo_AppHost>();

        _app = await builder.BuildAsync();
        await _app.StartAsync();

        // 2. Connect RCON using the Aspire testing extension
        //    GetEndpoint returns a Uri (e.g., tcp://localhost:25575)
        var rconUri = _app.GetEndpoint("minecraft", "rcon");
        await Rcon.ConnectAsync(rconUri.Host, rconUri.Port);
        await Rcon.AuthenticateAsync("minecraft");

        // 3. Wait for the worker to finish building the village
        await WaitForVillageBuildAsync();

        // 4. Capture BlueMap URL
        try
        {
            var blueMapUri = _app.GetEndpoint("minecraft", "world-map");
            BlueMapUrl = $"http://{blueMapUri.Host}:{blueMapUri.Port}";
        }
        catch
        {
            // BlueMap endpoint may not be available in all configurations
            BlueMapUrl = string.Empty;
        }

        IsReady = true;
    }

    /// <summary>
    /// Polls for a known block at the first structure origin every 5 seconds.
    /// The watchtower at index 0 places cobblestone at origin (10, -59, 0).
    /// When the block exists, the village build is complete.
    /// </summary>
    private async Task WaitForVillageBuildAsync()
    {
        var timeout = TimeSpan.FromMinutes(3);
        var pollInterval = TimeSpan.FromSeconds(5);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            try
            {
                // 'execute if block' returns empty string on match
                var result = await Rcon.SendCommandAsync(
                    "execute if block 10 -59 0 minecraft:cobblestone");

                if (string.IsNullOrEmpty(result))
                    return; // Block found — village is built
            }
            catch
            {
                // RCON may not be ready yet, keep polling
            }

            await Task.Delay(pollInterval);
        }

        throw new TimeoutException(
            "Village was not built within the 3-minute timeout. " +
            "Expected cobblestone at (10, -59, 0).");
    }

    public async Task DisposeAsync()
    {
        try { await Rcon.DisposeAsync(); } catch { /* best-effort cleanup */ }

        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
