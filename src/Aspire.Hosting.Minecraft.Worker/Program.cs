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

builder.Services.AddHttpClient("aspire-monitor")
    .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromSeconds(30),
        SslOptions = new System.Net.Security.SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = delegate { return true; }
        }
    });

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

// Minecart rail network — register service when enabled
if (builder.Configuration["ASPIRE_FEATURE_MINECART_RAILS"] == "true")
{
    builder.Services.AddSingleton<MinecartRailService>();
}

// Water canal network — register service when enabled
if (builder.Configuration["ASPIRE_FEATURE_CANALS"] == "true")
{
    builder.Services.AddSingleton<CanalService>();
    builder.Services.AddSingleton<BridgeService>();
}

// Error boat visualization — register service when enabled
if (builder.Configuration["ASPIRE_FEATURE_ERROR_BOATS"] == "true")
{
    builder.Services.AddSingleton<ErrorBoatService>();
}

// Neighborhood mode flag — checked in worker after resource discovery

// Services
builder.Services.AddSingleton<AspireResourceMonitor>();
builder.Services.AddSingleton<PlayerMessageService>();
builder.Services.AddSingleton<HologramManager>();
builder.Services.AddSingleton<ScoreboardManager>();
builder.Services.AddSingleton<BuildingProtectionService>();
builder.Services.AddSingleton<StructureBuilder>();
builder.Services.AddSingleton<TerrainProbeService>();
builder.Services.AddSingleton<GrandObservationTowerService>();
builder.Services.AddSingleton<HorseSpawnService>();
builder.Services.AddSingleton<VillagerService>();

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
    RedstoneDashboardService? redstoneDashboard = null,
    MinecartRailService? minecartRails = null,
    CanalService? canals = null,
    BridgeService? bridges = null,
    ErrorBoatService? errorBoats = null,
    VillagerService? villagers = null,
    GrandObservationTowerService? observationTower = null) : BackgroundService
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

        // Minimal forceload around terrain probe point so DetectSurfaceAsync can work.
        // The full village forceload happens AFTER neighborhood planning below.
        await rcon.SendCommandAsync(
            $"forceload add {VillageLayout.BaseX - 5} {VillageLayout.BaseZ - 5} {VillageLayout.BaseX + 5} {VillageLayout.BaseZ + 5}",
            stoppingToken);

        // Detect terrain surface height before building anything
        await terrainProbe.DetectSurfaceAsync(stoppingToken);

        // Discover Aspire resources
        resourceMonitor.DiscoverResources();

        // Plan neighborhoods when enabled — must happen after resource discovery
        // and BEFORE forceload so the forceload covers the actual neighborhood bounds.
        if (Environment.GetEnvironmentVariable("ASPIRE_FEATURE_NEIGHBORHOODS") == "true"
            && resourceMonitor.Resources.Count > 0)
        {
            VillageLayout.PlanNeighborhoods(resourceMonitor.Resources);
            logger.LogInformation("Neighborhood layout planned: {ZoneCount} zones for {ResourceCount} resources",
                VillageLayout.ActiveNeighborhoodPlan?.Neighborhoods.Count ?? 0,
                resourceMonitor.Resources.Count);
        }

        // Force-load the village chunks so block commands work before any player joins.
        // MUST happen AFTER PlanNeighborhoods — neighborhoods spread buildings across
        // 4 quadrants with ZoneGap, making the village much wider than the standard grid.
        // Using the actual resource count (not hardcoded 10) ensures correct bounds.
        var actualResourceCount = Math.Max(resourceMonitor.Resources.Count, 10);
        var (flMinX, flMinZ, flMaxX, flMaxZ) = VillageLayout.GetFencePerimeter(actualResourceCount);
        await rcon.SendCommandAsync(
            $"forceload add {flMinX - 10} {flMinZ - 10} {flMaxX + 10} {flMaxZ + 10}", stoppingToken);
        logger.LogInformation("Village chunks force-loaded: ({MinX},{MinZ}) to ({MaxX},{MaxZ})",
            flMinX - 10, flMinZ - 10, flMaxX + 10, flMaxZ + 10);

        // Force-load canal and lake chunks when canals feature is enabled.
        // These areas extend beyond the village fence perimeter and must be loaded
        // for /fill commands to succeed.
        if (canals is not null)
        {
            var canalResourceCount = resourceMonitor.Resources.Count;
            if (canalResourceCount > 0)
            {
                // Canal area: west of village grid to trunk canal
                var (canalMinX, _, _, _) = VillageLayout.GetVillageBounds(canalResourceCount);
                var trunkX = canalMinX - VillageLayout.CanalTotalWidth - 2;
                var firstEntrance = VillageLayout.GetCanalEntrance(0);
                var lastEntrance = VillageLayout.GetCanalEntrance(canalResourceCount - 1);
                var canalMinZ = Math.Min(firstEntrance.z, lastEntrance.z) - VillageLayout.CanalTotalWidth;
                var canalMaxZ = VillageLayout.GetLakePosition(canalResourceCount).z;
                await rcon.SendCommandAsync(
                    $"forceload add {trunkX - VillageLayout.CanalTotalWidth} {canalMinZ} {canalMinX} {canalMaxZ + VillageLayout.LakeLength}",
                    stoppingToken);

                // Lake area: south of village (now much larger, ~80x40 blocks)
                var (lakeX, _, lakeZ) = VillageLayout.GetLakePosition(canalResourceCount);
                await rcon.SendCommandAsync(
                    $"forceload add {lakeX - 10} {lakeZ - 5} {lakeX + VillageLayout.LakeWidth + 10} {lakeZ + VillageLayout.LakeLength + 5}",
                    stoppingToken);

                logger.LogInformation("Canal and lake chunks force-loaded");
            }
        }

        // Grand Observation Tower: forceload, register protection, and build.
        // Must happen AFTER terrain probe and neighborhoods, BEFORE structures.
        if (observationTower is not null)
        {
            observationTower.SetPosition(actualResourceCount);
            await observationTower.ForceloadAsync(stoppingToken);
            observationTower.RegisterProtection();
            await observationTower.BuildTowerAsync(stoppingToken);
        }

        // Initialize opt-in features that need startup commands
        // NOTE: Rails and canals are initialized AFTER structures are built (see main loop)
        // to avoid being paved over by building foundations and paths.
        if (worldBorder is not null)
            await worldBorder.InitializeAsync(stoppingToken);
        if (redstoneGraph is not null)
            await redstoneGraph.InitializeAsync(stoppingToken);
        if (redstoneDashboard is not null)
            await redstoneDashboard.InitializeAsync(stoppingToken);
        if (errorBoats is not null)
            await errorBoats.InitializeAsync(stoppingToken);

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
                    if (errorBoats is not null)
                        await errorBoats.SpawnBoatsForChangesAsync(changes, stoppingToken);
                }

                // Achievement checks that run every cycle (e.g., Night Shift needs time query)
                if (achievements is not null && changes.Count == 0)
                    await achievements.CheckAchievementsAsync(changes, stoppingToken);

                // Update in-world displays
                await holograms.UpdateDashboardAsync(stoppingToken);
                await scoreboard.UpdateScoreboardAsync(stoppingToken);
                await structures.UpdateStructuresAsync(stoppingToken);
                await horseSpawn.SpawnHorsesAsync(stoppingToken);
                if (villagers is not null)
                {
                    villagers.SetResourceCount(actualResourceCount);
                    await villagers.SpawnVillagersAsync(stoppingToken);
                }

                // Build canals first so CanalPositions is populated for bridge and rail detection,
                // then walkway bridges, then rails. All run AFTER structures so they aren't paved over.
                if (canals is not null)
                    await canals.InitializeAsync(stoppingToken);
                if (bridges is not null)
                    await bridges.InitializeAsync(stoppingToken);
                if (minecartRails is not null)
                    await minecartRails.InitializeAsync(stoppingToken);

                // After canals are built, replay any buffered error boat spawns
                // (ErrorBoatService buffers health changes that arrived before canals were ready)
                if (errorBoats is not null && canals is not null && canals.CanalPositions.Count > 0)
                    await errorBoats.SpawnBoatsForChangesAsync(Array.Empty<ResourceStatusChange>(), stoppingToken);

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
                if (redstoneGraph is not null)
                    await redstoneGraph.UpdateAsync(stoppingToken);
                if (serviceSwitches is not null)
                    await serviceSwitches.UpdateAsync(stoppingToken);
                if (redstoneDashboard is not null)
                    await redstoneDashboard.UpdateAsync(stoppingToken);
                if (minecartRails is not null)
                    await minecartRails.UpdateAsync(stoppingToken);
                if (canals is not null)
                    await canals.UpdateAsync(stoppingToken);
                if (bridges is not null)
                    await bridges.UpdateAsync(stoppingToken);
                if (errorBoats is not null)
                    await errorBoats.CleanupBoatsAsync(stoppingToken);

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
