using Aspire.Hosting.Minecraft.Rcon;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Managed RCON service for the worker. Wraps RconConnection with metrics, tracing,
/// command throttling, and priority-based rate limiting to prevent flooding the server
/// during rapid health transitions or cascading failures.
/// </summary>
internal sealed class RconService : IAsyncDisposable
{
    private readonly RconConnection _connection;
    private readonly ILogger<RconService> _logger;
    private readonly TimeSpan _minCommandInterval;
    private readonly ConcurrentDictionary<string, DateTime> _lastCommandTimes = new();

    // Rate limiting: token bucket
    private readonly int _maxCommandsPerSecond;
    private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
    private int _tokenCount;
    private DateTime _lastTokenRefill = DateTime.UtcNow;

    // Command queue for low-priority commands when rate-limited
    private readonly Channel<QueuedCommand> _commandQueue;
    private readonly CancellationTokenSource _queueProcessorCts = new();
    private readonly Task _queueProcessorTask;

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
    /// <param name="maxCommandsPerSecond">
    /// Maximum RCON commands per second (rate limit). Defaults to 10.
    /// High-priority commands bypass this limit; low-priority commands queue.
    /// </param>
    public RconService(string host, int port, string password, ILogger<RconService> logger,
        TimeSpan? minCommandInterval = null, int maxCommandsPerSecond = 10)
    {
        _connection = new RconConnection(host, port, password, logger);
        _logger = logger;
        _minCommandInterval = minCommandInterval ?? TimeSpan.Zero;
        _maxCommandsPerSecond = maxCommandsPerSecond;
        _tokenCount = maxCommandsPerSecond;

        _commandQueue = Channel.CreateBounded<QueuedCommand>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _queueProcessorTask = ProcessCommandQueueAsync(_queueProcessorCts.Token);
    }

    public bool IsConnected => _connection.IsConnected;

    /// <summary>
    /// Sends a command to the Minecraft server, recording metrics and traces.
    /// Duplicate commands within the configured throttle interval are skipped.
    /// Uses <see cref="CommandPriority.Normal"/> priority.
    /// </summary>
    /// <param name="command">The RCON command to send.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The server's response, or an empty string if the command was throttled or queued.</returns>
    public Task<string> SendCommandAsync(string command, CancellationToken ct = default)
        => SendCommandAsync(command, CommandPriority.Normal, ct);

    /// <summary>
    /// Sends a command to the Minecraft server with the specified priority.
    /// High-priority commands bypass rate limits. Low-priority commands are queued
    /// when the rate limit is hit. Duplicate commands within the throttle interval are skipped.
    /// </summary>
    /// <param name="command">The RCON command to send.</param>
    /// <param name="priority">The priority level for this command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The server's response, or an empty string if the command was throttled or queued.</returns>
    public async Task<string> SendCommandAsync(string command, CommandPriority priority, CancellationToken ct = default)
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

        // Rate limiting: high-priority commands always go through
        if (priority != CommandPriority.High)
        {
            var hasToken = await TryConsumeTokenAsync();
            if (!hasToken)
            {
                if (priority == CommandPriority.Low)
                {
                    // Queue low-priority commands for later execution
                    _commandQueue.Writer.TryWrite(new QueuedCommand(command, ct));
                    _logger.LogDebug("RCON command queued (rate-limited, priority={Priority}): {Command}",
                        priority, command);
                    return string.Empty;
                }
                // Normal priority: wait briefly for a token
                await Task.Delay(100, ct);
            }
        }

        return await ExecuteCommandAsync(command, ct);
    }

    private async Task<string> ExecuteCommandAsync(string command, CancellationToken ct)
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

    private async Task<bool> TryConsumeTokenAsync()
    {
        await _rateLimitSemaphore.WaitAsync();
        try
        {
            RefillTokens();
            if (_tokenCount > 0)
            {
                _tokenCount--;
                return true;
            }
            return false;
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }

    private void RefillTokens()
    {
        var now = DateTime.UtcNow;
        var elapsed = now - _lastTokenRefill;
        var newTokens = (int)(elapsed.TotalSeconds * _maxCommandsPerSecond);
        if (newTokens > 0)
        {
            _tokenCount = Math.Min(_tokenCount + newTokens, _maxCommandsPerSecond);
            _lastTokenRefill = now;
        }
    }

    private async Task ProcessCommandQueueAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var queued in _commandQueue.Reader.ReadAllAsync(ct))
            {
                if (queued.CancellationToken.IsCancellationRequested) continue;

                try
                {
                    // Wait for a rate limit token before processing queued commands
                    while (!await TryConsumeTokenAsync())
                    {
                        await Task.Delay(100, ct);
                    }
                    await ExecuteCommandAsync(queued.Command, queued.CancellationToken);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Queued RCON command failed: {Command}", queued.Command);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    public async ValueTask DisposeAsync()
    {
        _queueProcessorCts.Cancel();
        _commandQueue.Writer.TryComplete();
        try { await _queueProcessorTask; } catch (OperationCanceledException) { }
        _queueProcessorCts.Dispose();
        _rateLimitSemaphore.Dispose();
        await _connection.DisposeAsync();
    }

    private record QueuedCommand(string Command, CancellationToken CancellationToken);
}
