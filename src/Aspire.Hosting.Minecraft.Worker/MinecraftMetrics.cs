using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Aspire.Hosting.Minecraft.Worker;

/// <summary>
/// Custom OTEL metrics for the Minecraft server — game-specific telemetry scraped via RCON.
/// </summary>
internal sealed class MinecraftMetrics
{
    public const string MeterName = "Aspire.Minecraft";
    public const string ActivitySourceName = "Aspire.Minecraft";

    public static readonly Meter Meter = new(MeterName);
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    // Game metrics (updated by RCON polling)
    private static double _tps = 20.0;
    private static double _mspt;
    private static int _playersOnline;
    private static int _playersMax = 20;
    private static int _worldsLoaded;

    // Operational counters
    private static readonly Counter<long> RconCommandsSent =
        Meter.CreateCounter<long>("minecraft.rcon.commands_sent", description: "RCON commands issued by the worker");

    private static readonly Histogram<double> RconLatency =
        Meter.CreateHistogram<double>("minecraft.rcon.latency_ms", "ms", "Round-trip time for RCON commands");

    private static readonly Counter<long> PlayerMessagesSent =
        Meter.CreateCounter<long>("minecraft.player_messages.sent", description: "System messages sent to players");

    static MinecraftMetrics()
    {
        Meter.CreateObservableGauge("minecraft.tps",
            () => _tps, description: "Server ticks per second (target: 20.0)");
        Meter.CreateObservableGauge("minecraft.mspt",
            () => _mspt, "ms", "Milliseconds per tick (target: <50ms)");
        Meter.CreateObservableGauge("minecraft.players.online",
            () => _playersOnline, description: "Online player count");
        Meter.CreateObservableGauge("minecraft.players.max",
            () => _playersMax, description: "Max player slots");
        Meter.CreateObservableGauge("minecraft.worlds.loaded",
            () => _worldsLoaded, description: "Number of loaded worlds");
    }

    public static void UpdateTps(double tps) => _tps = tps;
    public static void UpdateMspt(double mspt) => _mspt = mspt;
    public static void UpdatePlayers(int online, int max)
    {
        _playersOnline = online;
        _playersMax = max;
    }
    public static void UpdateWorldsLoaded(int count) => _worldsLoaded = count;

    public static void RecordRconCommand(double latencyMs)
    {
        RconCommandsSent.Add(1);
        RconLatency.Record(latencyMs);
    }

    public static void RecordPlayerMessage(string messageType)
    {
        PlayerMessagesSent.Add(1, new KeyValuePair<string, object?>("message_type", messageType));
    }
}
