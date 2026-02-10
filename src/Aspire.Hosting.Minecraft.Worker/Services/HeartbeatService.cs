using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Plays a rhythmic note block heartbeat whose tempo and pitch reflect overall fleet health.
/// Healthy = fast, high-pitched pulse. Degraded = slow, low-pitched. Dead = silence.
/// Runs on its own timing loop independent of the main worker poll interval.
/// </summary>
internal sealed class HeartbeatService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<HeartbeatService> logger) : BackgroundService
{
    private int _tick;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Heartbeat service starting, waiting for resources...");

        // Wait until resources are discovered
        while (monitor.TotalCount == 0 && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }

        logger.LogInformation("Heartbeat service active");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var healthPercent = monitor.TotalCount > 0
                    ? (int)((double)monitor.HealthyCount / monitor.TotalCount * 100)
                    : 0;

                var (interval, pitch) = healthPercent switch
                {
                    0 => (TimeSpan.Zero, 0f),            // flatline — silence
                    < 50 => (TimeSpan.FromSeconds(4), 0.7f),  // labored pulse
                    < 100 => (TimeSpan.FromSeconds(2), 1.0f), // slower beat
                    _ => (TimeSpan.FromSeconds(1), 1.5f)      // steady heartbeat
                };

                if (interval == TimeSpan.Zero)
                {
                    // Flatline — no sound, just wait and re-check
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                // Vary volume slightly each tick to avoid RCON 250ms deduplication throttle
                _tick++;
                var volume = 0.5 + (_tick % 10) * 0.001;
                await rcon.SendCommandAsync(
                    $"execute at @a run playsound minecraft:block.note_block.bass block @a ~ ~ ~ {volume:F3} {pitch:F1}",
                    stoppingToken);

                logger.LogDebug("Heartbeat pulse: {HealthPercent} percent, pitch={Pitch}, interval={Interval}s",
                    healthPercent, pitch, interval.TotalSeconds);

                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in heartbeat loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("Heartbeat service stopping");
    }
}
