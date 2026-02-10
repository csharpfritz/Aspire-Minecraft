using Aspire.Hosting.Minecraft.Rcon;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Managed RCON service for the worker. Wraps RconConnection with metrics and tracing.
/// </summary>
internal sealed class RconService : IAsyncDisposable
{
    private readonly RconConnection _connection;
    private readonly ILogger<RconService> _logger;

    public RconService(string host, int port, string password, ILogger<RconService> logger)
    {
        _connection = new RconConnection(host, port, password, logger);
        _logger = logger;
    }

    public bool IsConnected => _connection.IsConnected;

    /// <summary>
    /// Sends a command to the Minecraft server, recording metrics and traces.
    /// </summary>
    public async Task<string> SendCommandAsync(string command, CancellationToken ct = default)
    {
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
