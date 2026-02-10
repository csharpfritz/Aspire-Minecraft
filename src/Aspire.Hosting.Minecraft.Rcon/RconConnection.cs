using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Rcon;

/// <summary>
/// Managed RCON connection with auto-reconnect and exponential backoff.
/// </summary>
public sealed class RconConnection : IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _password;
    private readonly ILogger _logger;
    private RconClient? _client;
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);

    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="RconConnection"/> class.
    /// </summary>
    /// <param name="host">The hostname or IP address of the Minecraft RCON server.</param>
    /// <param name="port">The RCON port number.</param>
    /// <param name="password">The RCON authentication password.</param>
    /// <param name="logger">Logger for connection events and diagnostics.</param>
    public RconConnection(string host, int port, string password, ILogger logger)
    {
        _host = host;
        _port = port;
        _password = password;
        _logger = logger;
    }

    /// <summary>
    /// Gets a value indicating whether the connection is established and authenticated.
    /// </summary>
    public bool IsConnected => _client?.IsConnected == true;

    /// <summary>
    /// Sends a command, reconnecting if necessary.
    /// </summary>
    /// <param name="command">The RCON command to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The server's text response to the command.</returns>
    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        var client = await EnsureConnectedAsync(cancellationToken);
        try
        {
            return await client.SendCommandAsync(command, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            _logger.LogWarning(ex, "RCON connection lost, will reconnect on next command");
            await DisposeClientAsync();
            // Retry once after reconnect
            client = await EnsureConnectedAsync(cancellationToken);
            return await client.SendCommandAsync(command, cancellationToken);
        }
    }

    private async Task<RconClient> EnsureConnectedAsync(CancellationToken ct)
    {
        if (_client?.IsConnected == true) return _client;

        await _reconnectLock.WaitAsync(ct);
        try
        {
            if (_client?.IsConnected == true) return _client;

            await DisposeClientAsync();

            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    var client = new RconClient();
                    await client.ConnectAsync(_host, _port, ct);
                    var authed = await client.AuthenticateAsync(_password, ct);
                    if (!authed)
                        throw new InvalidOperationException("RCON authentication failed.");

                    _logger.LogInformation("RCON connected to {Host}:{Port}", _host, _port);
                    _client = client;
                    return client;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    var delay = BackoffDelays[Math.Min(attempt, BackoffDelays.Length - 1)];
                    _logger.LogWarning(ex, "RCON connection attempt {Attempt} failed, retrying in {Delay}s",
                        attempt + 1, delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                }
            }
        }
        finally
        {
            _reconnectLock.Release();
        }
    }

    private async Task DisposeClientAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
    }

    /// <summary>
    /// Disposes the RCON connection and releases all resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisposeClientAsync();
        _reconnectLock.Dispose();
    }
}
