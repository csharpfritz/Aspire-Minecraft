using Aspire.Hosting.Minecraft.Rcon;
using Aspire.Hosting.Minecraft.Worker;
using Aspire.Hosting.Minecraft.Worker.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

// OpenTelemetry setup
builder.Services.AddOpenTelemetry()
    .WithMetrics(m =>
    {
        m.AddMeter(MinecraftMetrics.MeterName);
        m.AddOtlpExporter();
    })
    .WithTracing(t =>
    {
        t.AddSource(MinecraftMetrics.ActivitySourceName);
        t.AddOtlpExporter();
    });

builder.Services.AddHttpClient();

// RCON connection configuration — supports both Aspire connection string and explicit config
builder.Services.AddSingleton(sp =>
{
    var config = builder.Configuration;
    var logger = sp.GetRequiredService<ILogger<RconService>>();

    // Try Aspire connection string first (ConnectionStrings__minecraft = "Host=...;Port=...;Password=...")
    var connectionString = config["ConnectionStrings:minecraft"];
    if (!string.IsNullOrEmpty(connectionString))
    {
        var parts = ParseConnectionString(connectionString);
        return new RconService(parts.host, parts.port, parts.password, logger,
            minCommandInterval: TimeSpan.FromMilliseconds(250));
    }

    // Fall back to explicit config
    var host = config["Minecraft:RconHost"] ?? "localhost";
    var port = int.Parse(config["Minecraft:RconPort"] ?? "25575");
    var password = config["Minecraft:RconPassword"] ?? "";
    return new RconService(host, port, password, logger,
        minCommandInterval: TimeSpan.FromMilliseconds(250));
});

// Grand Village mode — configure larger layout before any structure placement
if (builder.Configuration["ASPIRE_FEATURE_GRAND_VILLAGE"] == "true")
{
    VillageLayout.ConfigureGrandLayout();
}

// Services
builder.Services.AddSingleton<AspireResourceMonitor>();
builder.Services.AddSingleton<PlayerMessageService>();
builder.Services.AddSingleton<HologramManager>();
builder.Services.AddSingleton<ScoreboardManager>();
builder.Services.AddSingleton<StructureBuilder>();
builder.Services.AddSingleton<TerrainProbeService>();
builder.Services.AddSingleton<HorseSpawnService>();

// HealthHistoryTracker is always registered (cheap ring buffer used by dashboard if enabled)
builder.Services.AddSingleton<HealthHistoryTracker>();

// Opt-in features (enabled via env vars set by builder extension methods)
if (builder.Configuration["ASPIRE_FEATURE_PARTICLES"] == "true")
    builder.Services.AddSingleton<ParticleEffectService>();
if (builder.Configuration["ASPIRE_FEATURE_TITLE_ALERTS"] == "true")
    builder.Services.AddSingleton<TitleAlertService>();
if (builder.Configuration["ASPIRE_FEATURE_WEATHER"] == "true")
    builder.Services.AddSingleton<WeatherService>();
if (builder.Configuration["ASPIRE_FEATURE_BOSSBAR"] == "true")
    builder.Services.AddSingleton<BossBarService>();
if (builder.Configuration["ASPIRE_FEATURE_SOUNDS"] == "true")
    builder.Services.AddSingleton<SoundEffectService>();
if (builder.Configuration["ASPIRE_FEATURE_ACTIONBAR"] == "true")
    builder.Services.AddSingleton<ActionBarTickerService>();
if (builder.Configuration["ASPIRE_FEATURE_BEACONS"] == "true")
    builder.Services.AddSingleton<BeaconTowerService>();
if (builder.Configuration["ASPIRE_FEATURE_FIREWORKS"] == "true")
    builder.Services.AddSingleton<FireworksService>();
if (builder.Configuration["ASPIRE_FEATURE_GUARDIANS"] == "true")
    builder.Services.AddSingleton<GuardianMobService>();
if (builder.Configuration["ASPIRE_FEATURE_FANFARE"] == "true")
    builder.Services.AddSingleton<DeploymentFanfareService>();
if (builder.Configuration["ASPIRE_FEATURE_WORLDBORDER"] == "true")
    builder.Services.AddSingleton<WorldBorderService>();
if (builder.Configuration["ASPIRE_FEATURE_HEARTBEAT"] == "true")
    builder.Services.AddSingleton<HeartbeatService>();
if (builder.Configuration["ASPIRE_FEATURE_ACHIEVEMENTS"] == "true")
    builder.Services.AddSingleton<AdvancementService>();
if (builder.Configuration["ASPIRE_FEATURE_REDSTONE_GRAPH"] == "true")
    builder.Services.AddSingleton<RedstoneDependencyService>();
if (builder.Configuration["ASPIRE_FEATURE_SWITCHES"] == "true")
    builder.Services.AddSingleton<ServiceSwitchService>();
if (builder.Configuration["ASPIRE_FEATURE_REDSTONE_DASHBOARD"] == "true")
{
    builder.Services.AddSingleton<RedstoneDashboardService>();
}

// Background worker
builder.Services.AddHostedService<MinecraftWorldWorker>();

var host = builder.Build();
await host.RunAsync();

static (string host, int port, string password) ParseConnectionString(string cs)
{
    var host = "localhost";
    var port = 25575;
    var password = "";
    foreach (var part in cs.Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
        var kv = part.Split('=', 2);
        if (kv.Length != 2) continue;
        switch (kv[0].Trim().ToLowerInvariant())
        {
            case "host": host = kv[1].Trim(); break;
            case "port": port = int.Parse(kv[1].Trim()); break;
            case "password": password = kv[1].Trim(); break;
        }
    }
    return (host, port, password);
}

/// <summary>
/// Background service that polls game metrics and updates in-world displays.
/// </summary>
file sealed class MinecraftWorldWorker(
    RconService rcon,
    AspireResourceMonitor resourceMonitor,
    PlayerMessageService playerMessages,
    HologramManager holograms,
    ScoreboardManager scoreboard,
    StructureBuilder structures,
    TerrainProbeService terrainProbe,
    HorseSpawnService horseSpawn,
    ILogger<MinecraftWorldWorker> logger,
    ParticleEffectService? particles = null,
    TitleAlertService? titleAlerts = null,
    WeatherService? weather = null,
    BossBarService? bossBar = null,
    SoundEffectService? sounds = null,
    ActionBarTickerService? actionBarTicker = null,
    BeaconTowerService? beaconTowers = null,
    FireworksService? fireworks = null,
    GuardianMobService? guardianMobs = null,
    DeploymentFanfareService? deploymentFanfare = null,
    WorldBorderService? worldBorder = null,
    AdvancementService? achievements = null,
    HeartbeatService? heartbeat = null,
    RedstoneDependencyService? redstoneGraph = null,
    ServiceSwitchService? serviceSwitches = null,
    RedstoneDashboardService? redstoneDashboard = null) : BackgroundService
{
    private static readonly TimeSpan MetricsPollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DisplayUpdateInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StatusBroadcastInterval = TimeSpan.FromMinutes(2);
    private DateTime _lastStatusBroadcast = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Minecraft world worker starting, waiting for server...");

        // Wait for RCON to become available
        await WaitForServerAsync(stoppingToken);

        logger.LogInformation("Connected to Minecraft server via RCON");

        // Force-load the village chunks so block commands work before any player joins.
        // Covers a generous area around the village grid (BaseX=10, BaseZ=0) plus margins.
        await rcon.SendCommandAsync("forceload add -20 -20 120 120", stoppingToken);
        logger.LogInformation("Village chunks force-loaded");

        // Detect terrain surface height before building anything
        await terrainProbe.DetectSurfaceAsync(stoppingToken);

        // Discover Aspire resources
        resourceMonitor.DiscoverResources();

        // Initialize opt-in features that need startup commands
        if (worldBorder is not null)
            await worldBorder.InitializeAsync(stoppingToken);
        if (redstoneGraph is not null)
            await redstoneGraph.InitializeAsync(stoppingToken);
        if (redstoneDashboard is not null)
            await redstoneDashboard.InitializeAsync(stoppingToken);

        // Peaceful mode — eliminate hostile mobs (one-time setup)
        if (Environment.GetEnvironmentVariable("ASPIRE_FEATURE_PEACEFUL") == "true")
        {
            await rcon.SendCommandAsync("difficulty peaceful", stoppingToken);
            logger.LogInformation("Peaceful mode enabled — hostile mobs disabled");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Poll game metrics
                await PollGameMetricsAsync(stoppingToken);

                // Check resource health and notify on changes
                var changes = await resourceMonitor.PollHealthAsync(stoppingToken);
                foreach (var change in changes)
                {
                    await playerMessages.BroadcastHealthAlertAsync(
                        change.Name, change.OldStatus.ToString(), change.NewStatus.ToString(), stoppingToken);
                }

                // Opt-in features triggered on health transitions
                if (changes.Count > 0)
                {
                    if (particles is not null)
                        await particles.ShowParticlesForChangesAsync(changes, stoppingToken);
                    if (titleAlerts is not null)
                        await titleAlerts.ShowTitleAlertsAsync(changes, stoppingToken);
                    if (sounds is not null)
                        await sounds.PlaySoundsForChangesAsync(changes, stoppingToken);
                    if (fireworks is not null)
                        await fireworks.CheckAndLaunchFireworksAsync(changes, stoppingToken);
                    if (deploymentFanfare is not null)
                        await deploymentFanfare.CheckAndCelebrateAsync(changes, stoppingToken);
                    if (achievements is not null)
                        await achievements.CheckAchievementsAsync(changes, stoppingToken);
                    if (redstoneGraph is not null)
                        await redstoneGraph.UpdateAsync(stoppingToken);
                }

                // Achievement checks that run every cycle (e.g., Night Shift needs time query)
                if (achievements is not null && changes.Count == 0)
                    await achievements.CheckAchievementsAsync(changes, stoppingToken);

                // Update in-world displays
                await holograms.UpdateDashboardAsync(stoppingToken);
                await scoreboard.UpdateScoreboardAsync(stoppingToken);
                await structures.UpdateStructuresAsync(stoppingToken);
                await horseSpawn.SpawnHorsesAsync(stoppingToken);

                // Continuous fleet-health features(update every cycle, but only change on transitions)
                if (weather is not null)
                    await weather.UpdateWeatherAsync(stoppingToken);
                if (bossBar is not null)
                    await bossBar.UpdateBossBarAsync(stoppingToken);
                if (actionBarTicker is not null)
                    await actionBarTicker.TickAsync(stoppingToken);
                if (beaconTowers is not null)
                    await beaconTowers.UpdateBeaconTowersAsync(stoppingToken);
                if (guardianMobs is not null)
                    await guardianMobs.UpdateGuardianMobsAsync(stoppingToken);
                if (worldBorder is not null)
                    await worldBorder.UpdateWorldBorderAsync(stoppingToken);
                if (heartbeat is not null)
                    await heartbeat.PulseAsync(stoppingToken);
                if (serviceSwitches is not null)
                    await serviceSwitches.UpdateAsync(stoppingToken);
                if (redstoneDashboard is not null)
                    await redstoneDashboard.UpdateAsync(stoppingToken);

                // Periodic status broadcast
                if (DateTime.UtcNow - _lastStatusBroadcast > StatusBroadcastInterval)
                {
                    await playerMessages.BroadcastStatusSummaryAsync(
                        resourceMonitor.HealthyCount, resourceMonitor.TotalCount, stoppingToken);
                    _lastStatusBroadcast = DateTime.UtcNow;
                }

                await Task.Delay(DisplayUpdateInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in world worker loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("Minecraft world worker stopping");
    }

    private async Task PollGameMetricsAsync(CancellationToken ct)
    {
        try
        {
            // TPS
            var tpsResponse = await rcon.SendCommandAsync("tps", ct);
            var tps = RconResponseParser.ParseTps(tpsResponse);
            MinecraftMetrics.UpdateTps(tps.OneMinute);

            // MSPT
            var msptResponse = await rcon.SendCommandAsync("mspt", ct);
            var mspt = RconResponseParser.ParseMspt(msptResponse);
            MinecraftMetrics.UpdateMspt(mspt.FiveSecond);

            // Player list
            var listResponse = await rcon.SendCommandAsync("list", ct);
            var players = RconResponseParser.ParsePlayerList(listResponse);
            MinecraftMetrics.UpdatePlayers(players.Online, players.Max);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to poll game metrics");
        }
    }

    private async Task WaitForServerAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var response = await rcon.SendCommandAsync("list", ct);
                if (!string.IsNullOrEmpty(response)) return;
            }
            catch
            {
                // Server not ready yet
            }

            logger.LogInformation("Waiting for Minecraft server to be ready...");
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}
