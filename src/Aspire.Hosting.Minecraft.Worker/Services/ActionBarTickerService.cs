using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Cycles through key metrics on the action bar (HUD text above the hotbar).
/// Rotates: TPS, MSPT, healthy resource count, RCON latency every 2–3 seconds.
/// </summary>
internal sealed class ActionBarTickerService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<ActionBarTickerService> logger)
{
    private int _tickIndex;
    private readonly System.Diagnostics.Stopwatch _rconStopwatch = new();

    /// <summary>
    /// Sends the next metric to the action bar, cycling through available metrics.
    /// </summary>
    public async Task TickAsync(CancellationToken ct = default)
    {
        var message = _tickIndex switch
        {
            0 => await GetTpsMessageAsync(ct),
            1 => await GetMsptMessageAsync(ct),
            2 => GetHealthyCountMessage(),
            3 => await GetRconLatencyMessageAsync(ct),
            _ => ""
        };

        if (!string.IsNullOrEmpty(message))
        {
            await rcon.SendCommandAsync($"title @a actionbar \"{message}\"", ct);
            logger.LogDebug("Action bar ticker: {Message}", message);
        }

        _tickIndex = (_tickIndex + 1) % 4;
    }

    private async Task<string> GetTpsMessageAsync(CancellationToken ct)
    {
        try
        {
            var response = await rcon.SendCommandAsync("tps", ct);
            var tps = Rcon.RconResponseParser.ParseTps(response);
            return $"TPS: {tps.OneMinute:F1}/20.0";
        }
        catch
        {
            return "TPS: --";
        }
    }

    private async Task<string> GetMsptMessageAsync(CancellationToken ct)
    {
        try
        {
            var response = await rcon.SendCommandAsync("mspt", ct);
            var mspt = Rcon.RconResponseParser.ParseMspt(response);
            return $"MSPT: {mspt.FiveSecond:F1}ms";
        }
        catch
        {
            return "MSPT: --";
        }
    }

    private string GetHealthyCountMessage()
    {
        return $"Healthy: {monitor.HealthyCount}/{monitor.TotalCount} resources";
    }

    private async Task<string> GetRconLatencyMessageAsync(CancellationToken ct)
    {
        try
        {
            _rconStopwatch.Restart();
            await rcon.SendCommandAsync("list", ct);
            _rconStopwatch.Stop();
            return $"RCON Latency: {_rconStopwatch.ElapsedMilliseconds}ms";
        }
        catch
        {
            return "RCON Latency: --";
        }
    }
}
