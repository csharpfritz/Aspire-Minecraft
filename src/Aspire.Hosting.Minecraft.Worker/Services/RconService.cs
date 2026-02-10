using Aspire.Hosting.Minecraft.Rcon;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Managed RCON service for the worker. Wraps RconConnection with metrics, tracing,
/// and command throttling to prevent flooding the server during rapid health transitions.
/// </summary>
internal sealed class RconService : IAsyncDisposable
{
    private readonly RconConnection _connection;
    private readonly ILogger<RconService> _logger;
    private readonly TimeSpan _minCommandInterval;
    private readonly ConcurrentDictionary<string, DateTime> _lastCommandTimes = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RconService"/> class.
    /// </summary>
    /// <param name="host">The RCON server hostname.</param>
    /// <param name="port">The RCON server port.</param>
    /// <param name="password">The RCON password.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="minCommandInterval">
    /// Minimum interval between identical commands to prevent flooding.
    /// Defaults to <see cref="TimeSpan.Zero"/> (no throttling).
    /// Set to a positive value (e.g., 250ms) to debounce duplicate commands during rapid health transitions.
    /// </param>
    public RconService(string host, int port, string password, ILogger<RconService> logger, TimeSpan? minCommandInterval = null)
    {
        _connection = new RconConnection(host, port, password, logger);
        _logger = logger;
        _minCommandInterval = minCommandInterval ?? TimeSpan.Zero;
    }

    public bool IsConnected => _connection.IsConnected;

    /// <summary>
    /// Sends a command to the Minecraft server, recording metrics and traces.
    /// Duplicate commands within the configured throttle interval are skipped.
    /// </summary>
    /// <param name="command">The RCON command to send.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The server's response, or an empty string if the command was throttled.</returns>
    public async Task<string> SendCommandAsync(string command, CancellationToken ct = default)
    {
        // Throttle: skip duplicate commands sent within the minimum interval
        var now = DateTime.UtcNow;
        if (_lastCommandTimes.TryGetValue(command, out var lastTime)
            && (now - lastTime) < _minCommandInterval)
        {
            _logger.LogDebug("RCON command throttled (interval {Interval}ms): {Command}",
                _minCommandInterval.TotalMilliseconds, command);
            return string.Empty;
        }
        _lastCommandTimes[command] = now;

        using var activity = MinecraftMetrics.ActivitySource.StartActivity("minecraft.rcon.command");
        activity?.SetTag("rcon.command", command);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await _connection.SendCommandAsync(command, ct);
            sw.Stop();

            MinecraftMetrics.RecordRconCommand(sw.Elapsed.TotalMilliseconds);
            activity?.SetTag("rcon.response_length", response.Length);
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Ok);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "RCON command failed: {Command}", command);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
