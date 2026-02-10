using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Plays a rhythmic note block heartbeat whose tempo and pitch reflect overall fleet health.
/// Healthy = fast, high-pitched pulse. Degraded = slow, low-pitched. Dead = silence.
/// Called each cycle by MinecraftWorldWorker; manages its own internal timing to decide
/// whether to emit a pulse on each invocation.
/// </summary>
internal sealed class HeartbeatService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<HeartbeatService> logger)
{
    private int _tick;
    private DateTime _lastPulse = DateTime.MinValue;

    /// <summary>
    /// Called every worker cycle. Plays a heartbeat sound if enough time has elapsed
    /// since the last pulse, based on current fleet health.
    /// </summary>
    public async Task PulseAsync(CancellationToken ct = default)
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
                return; // flatline — no sound

            if (DateTime.UtcNow - _lastPulse < interval)
                return; // not time yet

            // Vary volume slightly each tick to avoid RCON 250ms deduplication throttle
            _tick++;
            var volume = 0.5 + (_tick % 10) * 0.001;
            await rcon.SendCommandAsync(
                $"execute at @a run playsound minecraft:block.note_block.bass block @a ~ ~ ~ {volume:F3} {pitch:F1}",
                ct);

            _lastPulse = DateTime.UtcNow;

            logger.LogDebug("Heartbeat pulse: {HealthPercent} percent, pitch={Pitch}, interval={Interval}s",
                healthPercent, pitch, interval.TotalSeconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in heartbeat pulse");
        }
    }
}
