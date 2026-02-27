using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Minecraft.Rcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting.Minecraft;

/// <summary>
/// Extension methods for adding and configuring Minecraft server resources in Aspire.
/// </summary>
public static class MinecraftServerBuilderExtensions
{
    private const string DefaultImage = "itzg/minecraft-server";
    private const string DefaultTag = "latest";

    /// <summary>
    /// Adds a Minecraft Paper server to the Aspire application.
    /// By default, world data is ephemeral — each run starts fresh. Call <see cref="WithPersistentWorld"/>
    /// to persist world data across restarts using a named Docker volume.
    /// Includes a built-in RCON health check so dependents can use WaitFor().
    /// </summary>
    /// <param name="builder">The Aspire distributed application builder.</param>
    /// <param name="name">A unique name for this Minecraft server resource.</param>
    /// <param name="gamePort">Optional external port for Minecraft game connections (default: auto-assigned, target 25565).</param>
    /// <param name="rconPort">Optional external port for RCON management connections (default: auto-assigned, target 25575).</param>
    /// <returns>A resource builder for further configuration of the Minecraft server.</returns>
    public static IResourceBuilder<MinecraftServerResource> AddMinecraftServer(
        this IDistributedApplicationBuilder builder,
        string name,
        int? gamePort = null,
        int? rconPort = null)
    {
        var rconPassword = builder.AddParameter($"{name}-rcon-password", secret: true, value: GeneratePassword());

        var resource = new MinecraftServerResource(name)
        {
            RconPasswordParameter = rconPassword.Resource
        };

        // Capture connection string when it becomes available (follows Redis pattern)
        string? connectionString = null;
        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(resource, async (@event, ct) =>
        {
            connectionString = await ((IResourceWithConnectionString)resource).GetConnectionStringAsync(ct).ConfigureAwait(false);
        });

        // Register RCON health check
        var healthCheckKey = $"{name}_rcon_check";
        builder.Services.AddHealthChecks().AddCheck(
            healthCheckKey,
            new MinecraftHealthCheck(() => connectionString));

        return builder.AddResource(resource)
            .WithImage(DefaultImage, DefaultTag)
            .WithEndpoint(port: gamePort, targetPort: 25565, name: MinecraftServerResource.GameEndpointName, scheme: "tcp")
            .WithEndpoint(port: rconPort, targetPort: 25575, name: MinecraftServerResource.RconEndpointName, scheme: "tcp")
            .WithEnvironment("EULA", "TRUE")
            .WithEnvironment("TYPE", "PAPER")
            .WithEnvironment("ONLINE_MODE", "FALSE")
            .WithEnvironment("MODE", "creative")
            .WithEnvironment("LEVEL_TYPE", "flat")
            .WithEnvironment("GENERATOR_SETTINGS", "")
            .WithEnvironment("SEED", "aspire2026")
            .WithEnvironment("ENABLE_RCON", "true")
            .WithEnvironment("RCON_PORT", "25575")
            // Startup performance: skip unnecessary work
            .WithEnvironment("SPAWN_PROTECTION", "0")
            .WithEnvironment("VIEW_DISTANCE", "12")
            .WithEnvironment("SIMULATION_DISTANCE", "8")
            .WithEnvironment("GENERATE_STRUCTURES", "false")
            .WithEnvironment("SPAWN_ANIMALS", "FALSE")
            .WithEnvironment("SPAWN_MONSTERS", "FALSE")
            .WithEnvironment("SPAWN_NPCS", "FALSE")
            .WithEnvironment("MAX_WORLD_SIZE", "29999984")
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["RCON_PASSWORD"] = rconPassword.Resource;
            })
            .WithHealthCheck(healthCheckKey)
            .WithLifetime(ContainerLifetime.Session);
    }

    /// <summary>
    /// Switches the Minecraft server to use a pre-baked Docker image that already contains
    /// BlueMap, DecentHolograms, the OTEL agent, and other plugins.
    /// When this is set, methods like <see cref="WithBlueMap"/> skip redundant bind-mounts
    /// (e.g., <c>core.conf</c>) because the files are already in the image.
    /// The default env vars (EULA, TYPE, etc.) are still applied — they're harmless overrides.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="imageName">The Docker image name. Defaults to <c>"aspire-minecraft-server"</c>.</param>
    /// <param name="tag">The image tag. Defaults to <c>"latest"</c>.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithPrebakedImage(
        this IResourceBuilder<MinecraftServerResource> builder,
        string imageName = "aspire-minecraft-server",
        string tag = "latest")
    {
        return builder
            .WithImage(imageName, tag)
            .WithEnvironment("ASPIRE_MINECRAFT_PREBAKED", "true")
            .WithAnnotation(new PrebakedImageAnnotation());
    }

    /// <summary>
    /// Persists the Minecraft world data across container restarts using a named Docker volume.
    /// Without this, each run starts with a fresh world (default behavior).
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithPersistentWorld(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        return builder.WithVolume($"{builder.Resource.Name}-data", "/data");
    }

    /// <summary>
    /// Marks all existing Minecraft server endpoints as externally accessible so that remote
    /// machines can connect. This modifies the game and RCON endpoints (and BlueMap, if present)
    /// rather than adding duplicate endpoints.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithExternalAccess(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var externalEndpointNames = new HashSet<string>
        {
            MinecraftServerResource.GameEndpointName,
            MinecraftServerResource.RconEndpointName,
            MinecraftServerResource.BlueMapEndpointName
        };

        foreach (var annotation in builder.Resource.Annotations.OfType<EndpointAnnotation>())
        {
            if (externalEndpointNames.Contains(annotation.Name))
            {
                annotation.IsExternal = true;
            }
        }

        return builder;
    }

    /// <summary>
    /// Adds BlueMap web map plugin, exposing its web UI as an HTTP endpoint in the Aspire dashboard.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="port">Optional external port for the BlueMap web UI (default: auto-assigned, target 8100).</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithBlueMap(
        this IResourceBuilder<MinecraftServerResource> builder,
        int? port = null)
    {
        // Resolve the bundled core.conf that has accept-download: true
        var assemblyDir = Path.GetDirectoryName(typeof(MinecraftServerBuilderExtensions).Assembly.Location)!;
        var coreConfPath = Path.Combine(assemblyDir, "bluemap", "core.conf");

        var result = builder
            .WithEndpoint(port: port, targetPort: 8100, name: MinecraftServerResource.BlueMapEndpointName, scheme: "http")
            .WithUrlForEndpoint(MinecraftServerResource.BlueMapEndpointName, url => url.DisplayText = "World Map")
            .WithAnnotation(new ModrinthPluginAnnotation("bluemap"))
            .WithEnvironment(context =>
            {
                AppendModrinthProject(context, "bluemap");
            });

        // Bind-mount the core.conf into /plugins/BlueMap/ so itzg copies it to
        // /data/plugins/BlueMap/core.conf before the server starts.
        // This ensures accept-download: true is set on first boot.
        // Skip when using a pre-baked image — the file is already in the image.
        var isPrebaked = builder.Resource.Annotations.OfType<PrebakedImageAnnotation>().Any();
        if (!isPrebaked && File.Exists(coreConfPath))
        {
            result = result.WithBindMount(coreConfPath, "/plugins/BlueMap/core.conf", isReadOnly: true);
        }

        return result;
    }

    /// <summary>
    /// Adds OpenTelemetry configuration to the Minecraft server.
    /// Bind-mounts the bundled OTEL Java agent JAR and configures the JVM to use it.
    /// Telemetry (metrics, traces, logs) is exported to the Aspire dashboard's OTLP endpoint.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithOpenTelemetry(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var assemblyDir = Path.GetDirectoryName(typeof(MinecraftServerBuilderExtensions).Assembly.Location)!;
        var agentJarPath = Path.Combine(assemblyDir, "otel", "opentelemetry-javaagent.jar");

        var result = builder
            .WithEnvironment(context =>
            {
                // Resolve the OTLP HTTP endpoint, replacing localhost with host.docker.internal.
                // Always use http:// even if the dashboard is configured for https, because
                // the OTEL Java agent can't validate the Aspire dev certificate.
                var otlpHttpEndpoint = Environment.GetEnvironmentVariable("DOTNET_DASHBOARD_OTLP_HTTP_ENDPOINT_URL");
                if (!string.IsNullOrEmpty(otlpHttpEndpoint))
                {
                    otlpHttpEndpoint = otlpHttpEndpoint
                        .Replace("://localhost:", "://host.docker.internal:")
                        .Replace("https://", "http://");
                }
                else
                {
                    otlpHttpEndpoint = "http://host.docker.internal:18890";
                }

                context.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = otlpHttpEndpoint;
                context.EnvironmentVariables["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf";
                context.EnvironmentVariables["OTEL_SERVICE_NAME"] = builder.Resource.Name;
                context.EnvironmentVariables["OTEL_LOGS_EXPORTER"] = "otlp";
                context.EnvironmentVariables["OTEL_METRICS_EXPORTER"] = "otlp";
                context.EnvironmentVariables["OTEL_TRACES_EXPORTER"] = "otlp";
            });

        // Bind-mount the OTEL Java agent and configure JVM to use it
        if (File.Exists(agentJarPath))
        {
            result = result
                .WithBindMount(agentJarPath, "/otel/opentelemetry-javaagent.jar", isReadOnly: true)
                .WithEnvironment("JVM_OPTS", "-javaagent:/otel/opentelemetry-javaagent.jar");
        }

        return result;
    }

    /// <summary>
    /// Adds DecentHolograms plugin for in-world hologram displays.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithDecentHolograms(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        return builder
            .WithAnnotation(new ModrinthPluginAnnotation("decentholograms"))
            .WithEnvironment(context =>
            {
                AppendModrinthProject(context, "decentholograms");
            });
    }

    /// <summary>
    /// Enables in-world Aspire resource display (holograms, scoreboards, torch structures).
    /// Internally registers the bundled worker service that connects via RCON to render Aspire state in Minecraft.
    /// The worker appears as a child of the Minecraft resource in the Aspire dashboard.
    /// Call WithMonitoredResource() after this to add resources to display.
    /// </summary>
    /// <typeparam name="TWorkerProject">The worker project type that implements the in-world display logic.</typeparam>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithAspireWorldDisplay<TWorkerProject>(
        this IResourceBuilder<MinecraftServerResource> builder)
        where TWorkerProject : IProjectMetadata, new()
    {
        // Add DecentHolograms plugin (required for holograms)
        builder = builder.WithDecentHolograms();

        // Internally create the worker project resource
        var workerName = $"{builder.Resource.Name}-worker";
        var workerBuilder = builder.ApplicationBuilder.AddProject<TWorkerProject>(workerName)
            .WithHttpEndpoint(name: "http")
            .WithReference(builder)
            .WaitFor(builder)
            .WithParentRelationship(builder);

        // Store the worker builder on the resource so WithMonitoredResource() can apply env vars
        builder.Resource.WorkerBuilder = workerBuilder;

        builder.WithAnnotation(new AspireWorldDisplayAnnotation());

        return builder;
    }

    private static void AppendModrinthProject(EnvironmentCallbackContext context, string slug)
    {
        if (context.EnvironmentVariables.TryGetValue("MODRINTH_PROJECTS", out var existing) && existing is string s && !string.IsNullOrEmpty(s))
        {
            context.EnvironmentVariables["MODRINTH_PROJECTS"] = $"{s}\n{slug}";
        }
        else
        {
            context.EnvironmentVariables["MODRINTH_PROJECTS"] = slug;
        }
    }

    private static string GeneratePassword()
    {
        return Guid.NewGuid().ToString("N")[..16];
    }

    /// <summary>
    /// Adds an Aspire resource to be monitored and displayed in the Minecraft world.
    /// The worker will create a cube, hologram entry, and scoreboard line for this resource.
    /// Works with any resource type — projects, containers, Redis, databases, etc.
    /// Must be called after WithAspireWorldDisplay().
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="resource">The Aspire resource with endpoints to monitor.</param>
    /// <param name="dependsOn">Optional list of resource names this resource depends on. Dependent resources are placed adjacent in the village grid.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithMonitoredResource(
        this IResourceBuilder<MinecraftServerResource> builder,
        IResourceBuilder<IResourceWithEndpoints> resource,
        params string[] dependsOn)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithMonitoredResource() requires WithAspireWorldDisplay() to be called first.");

        var name = resource.Resource.Name;
        builder.Resource.MonitoredResourceNames.Add(name);

        // Determine resource type from the concrete type.
        // Use GetType().Name so subclasses (RedisResource, PostgresServerResource, etc.)
        // get their specific type name instead of the base "Container" or "Project".
        var resourceType = resource.Resource.GetType().Name.Replace("Resource", "");

        workerBuilder.WithEnvironment($"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_TYPE", resourceType);

        // Collect dependency names: explicit params + IResourceWithParent auto-detection
        var allDeps = new List<string>(dependsOn);
        if (resource.Resource is IResourceWithParent parentResource)
        {
            var parentName = parentResource.Parent.Name;
            if (!allDeps.Contains(parentName, StringComparer.OrdinalIgnoreCase))
                allDeps.Add(parentName);
        }

        if (allDeps.Count > 0)
        {
            workerBuilder.WithEnvironment(
                $"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_DEPENDS_ON",
                string.Join(",", allDeps));
        }

        // Resolve endpoints for health checking.
        // GetEndpoint() never throws — it creates a dangling reference for missing endpoints.
        // We must iterate GetEndpoints() to find actually-existing endpoints.
        //
        // IMPORTANT: Skip endpoint resolution for ExecutableResource subclasses (PythonApp, NodeApp, etc.)
        // because their DCP-proxied endpoints are not reachable from the worker container's network context.
        // Resources without endpoints are assumed healthy, matching Aspire dashboard behavior.
        var isExecutable = resourceType.Contains("PythonApp", StringComparison.OrdinalIgnoreCase)
            || resourceType.Contains("NodeApp", StringComparison.OrdinalIgnoreCase)
            || resourceType.Contains("JavaScriptApp", StringComparison.OrdinalIgnoreCase)
            || resourceType.Contains("JavaAppExecutable", StringComparison.OrdinalIgnoreCase)
            || resourceType.Contains("Executable", StringComparison.OrdinalIgnoreCase);

        if (!isExecutable)
        {
            workerBuilder.WithEnvironment(context =>
            {
                if (resource.Resource is IResourceWithEndpoints resourceWithEndpoints)
                {
                    EndpointReference? httpRef = null;
                    EndpointReference? firstRef = null;
                    foreach (var ep in resourceWithEndpoints.GetEndpoints())
                    {
                        firstRef ??= ep;
                        if (string.Equals(ep.EndpointName, "http", StringComparison.OrdinalIgnoreCase))
                        {
                            httpRef = ep;
                            break;
                        }
                        if (string.Equals(ep.EndpointName, "https", StringComparison.OrdinalIgnoreCase))
                        {
                            httpRef = ep;
                        }
                    }

                    if (httpRef is not null)
                    {
                        // HTTP/HTTPS endpoint — use URL for HTTP health check
                        context.EnvironmentVariables[$"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_URL"] =
                            httpRef.Property(EndpointProperty.Url);
                        
                        // If the resource has a health check annotation, extract the path
                        if (resource.Resource.TryGetLastAnnotation<HealthCheckAnnotation>(out var healthCheck))
                        {
                            // Health check path defaults to "/health" if not specified
                            var healthPath = healthCheck.Key ?? "/health";
                            context.EnvironmentVariables[$"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_HEALTH_PATH"] = healthPath;
                        }
                    }
                    else if (firstRef is not null)
                    {
                        // Non-HTTP endpoint (Redis, databases, etc.) — pass host:port for TCP check
                        context.EnvironmentVariables[$"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_HOST"] =
                            firstRef.Property(EndpointProperty.Host);
                        context.EnvironmentVariables[$"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_PORT"] =
                            firstRef.Property(EndpointProperty.Port);
                    }
                }
            });
        }

        // Set the error notification webhook URL on the monitored service so it can
        // notify the worker when OpenTelemetry error-level log entries occur.
        // Only set for resources that have HTTP endpoints (they run the .NET OTel SDK).
        if (!isExecutable)
        {
            var workerHttp = new EndpointReference(workerBuilder.Resource, "http");
            resource.Resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables["ASPIRE_MINECRAFT_ERROR_WEBHOOK"] =
                    ReferenceExpression.Create($"{workerHttp.Property(EndpointProperty.Url)}/error-notification");
            }));
        }

        return builder;
    }

    /// <summary>
    /// Adds an Aspire resource to be monitored (for resources without endpoints).
    /// The resource will appear in the Minecraft world but health won't be polled via HTTP.
    /// Must be called after WithAspireWorldDisplay().
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="resource">The Aspire resource to monitor.</param>
    /// <param name="resourceType">A display label for the type of resource (e.g., "Database", "Cache").</param>
    /// <param name="dependsOn">Optional list of resource names this resource depends on.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithMonitoredResource(
        this IResourceBuilder<MinecraftServerResource> builder,
        IResourceBuilder<IResource> resource,
        string resourceType,
        params string[] dependsOn)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithMonitoredResource() requires WithAspireWorldDisplay() to be called first.");

        var name = resource.Resource.Name;
        builder.Resource.MonitoredResourceNames.Add(name);
        workerBuilder.WithEnvironment($"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_TYPE", resourceType);

        // Collect dependency names: explicit params + IResourceWithParent auto-detection
        var allDeps = new List<string>(dependsOn);
        if (resource.Resource is IResourceWithParent parentResource)
        {
            var parentName = parentResource.Parent.Name;
            if (!allDeps.Contains(parentName, StringComparer.OrdinalIgnoreCase))
                allDeps.Add(parentName);
        }

        if (allDeps.Count > 0)
        {
            workerBuilder.WithEnvironment(
                $"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_DEPENDS_ON",
                string.Join(",", allDeps));
        }

        return builder;
    }
    /// <summary>
    /// Enables particle effects at resource structures on health transitions.
    /// Crash: large_smoke + flame, Recovery: happy_villager particles appear at the resource's structure.
    /// Requires WithAspireWorldDisplay() to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithParticleEffects(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithParticleEffects() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_PARTICLES", "true");
        return builder;
    }

    /// <summary>
    /// Enables dramatic full-screen title alerts when resources go down or recover.
    /// Red "⚠ SERVICE DOWN" on failure, green "✅ BACK ONLINE" on recovery.
    /// Requires WithAspireWorldDisplay() to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithTitleAlerts(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithTitleAlerts() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_TITLE_ALERTS", "true");
        return builder;
    }

    /// <summary>
    /// Links Minecraft weather to overall Aspire fleet health.
    /// Clear when all healthy, rain when degraded, thunder when critical.
    /// Only changes weather on state transitions, not every poll cycle.
    /// Requires WithAspireWorldDisplay() to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithWeatherEffects(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithWeatherEffects() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_WEATHER", "true");
        return builder;
    }

    /// <summary>
    /// Adds a persistent boss bar showing overall Aspire fleet health percentage.
    /// Green = all healthy, yellow = degraded, red = majority down.
    /// Value 0–100 based on healthy/total resource count.
    /// The boss bar title includes the Aspire application name if configured.
    /// Requires WithAspireWorldDisplay() to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="title">Optional custom title for the boss bar. Defaults to "Aspire Fleet Health".</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithBossBar(
        this IResourceBuilder<MinecraftServerResource> builder,
        string? title = null)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithBossBar() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_BOSSBAR", "true");
        if (!string.IsNullOrEmpty(title))
            workerBuilder.WithEnvironment("ASPIRE_BOSSBAR_TITLE", title);
        return builder;
    }

    /// <summary>
    /// Enables sound effects on health state transitions.
    /// Down: entity.wither.ambient, Up: entity.player.levelup, All green: ui.toast.challenge_complete.
    /// Requires WithAspireWorldDisplay() to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithSoundEffects(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithSoundEffects() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_SOUNDS", "true");
        return builder;
    }

    /// <summary>
    /// Enables an action bar ticker that cycles through key metrics (TPS, MSPT, healthy count, RCON latency).
    /// Metrics rotate every poll cycle on the player's HUD above the hotbar.
    /// Requires WithAspireWorldDisplay() to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithActionBarTicker(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithActionBarTicker() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_ACTIONBAR", "true");
        return builder;
    }

    /// <summary>
    /// Enables beacon towers per monitored resource. Each resource gets an iron base with a beacon
    /// and stained glass on top. Green glass = healthy, red glass = unhealthy.
    /// Requires WithAspireWorldDisplay() to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithBeaconTowers(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithBeaconTowers() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_BEACONS", "true");
        return builder;
    }

    /// <summary>
    /// Enables fireworks when all monitored resources recover to healthy after a failure.
    /// Fireworks are launched at several positions around the resource area.
    /// Requires WithAspireWorldDisplay() to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithFireworks(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithFireworks() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_FIREWORKS", "true");
        return builder;
    }

    /// <summary>
    /// Spawns guardian mobs per monitored resource. Healthy resources get an iron golem;
    /// unhealthy resources get a zombie. Mobs are named after their resource.
    /// Requires WithAspireWorldDisplay() to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithGuardianMobs(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithGuardianMobs() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_GUARDIANS", "true");
        return builder;
    }

    /// <summary>
    /// Enables deployment fanfare when a resource transitions from Starting to Running.
    /// Includes lightning bolt, fireworks, and a title announcement.
    /// Requires WithAspireWorldDisplay() to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithDeploymentFanfare(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithDeploymentFanfare() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_FANFARE", "true");
        return builder;
    }

    /// <summary>
    /// Enables world border pulse on critical fleet health failure.
    /// When more than 50% of monitored resources are unhealthy, the world border shrinks
    /// from 200 to 100 blocks over 10 seconds with a red warning tint.
    /// When health recovers, the border expands back to 200 blocks over 5 seconds.
    /// Requires <see cref="WithAspireWorldDisplay{TWorkerProject}"/> to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithWorldBorderPulse(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithWorldBorderPulse() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_WORLDBORDER", "true");
        return builder;
    }

    /// <summary>
    /// Enables in-world achievement announcements for infrastructure milestones.
    /// Grants achievements such as "First Service Online", "Full Fleet Healthy",
    /// "Survived a Crash", and "Night Shift" using title popups and sounds.
    /// Each achievement is granted once per session.
    /// Requires <see cref="WithAspireWorldDisplay{TWorkerProject}"/> to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithAchievements(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithAchievements() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_ACHIEVEMENTS", "true");
        return builder;
    }

    /// <summary>
    /// Enables a redstone heartbeat circuit — an audible note block pulse whose tempo and pitch
    /// reflect overall fleet health. Healthy = steady fast rhythm, degraded = slow labored pulse,
    /// dead = silence. Runs on its own timing loop independent of the main display update interval.
    /// Requires <see cref="WithAspireWorldDisplay{TWorkerProject}"/> to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithHeartbeat(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithHeartbeat() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_HEARTBEAT", "true");
        return builder;
    }

    /// <summary>
    /// Enables redstone dependency graph visualization — draws redstone wire circuits between
    /// dependent resource structures in the Minecraft world. Wire paths show the resource DAG,
    /// with repeaters every 15 blocks and redstone lamps at structure entrances. When a resource
    /// goes unhealthy, its outgoing redstone connections break (lamps go dark). On recovery,
    /// circuits are restored. Runs on its own timing loop as a BackgroundService.
    /// Requires <see cref="WithAspireWorldDisplay{TWorkerProject}"/> to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithRedstoneDependencyGraph(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithRedstoneDependencyGraph() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_REDSTONE_GRAPH", "true");
        return builder;
    }

    /// <summary>
    /// Enables visual service switches — places Minecraft levers and redstone lamps on each
    /// resource structure to represent service status. Healthy = lever ON, lamp lit.
    /// Unhealthy = lever OFF, lamp dark. This is visual only — levers reflect state,
    /// they do not control Aspire resources.
    /// Requires <see cref="WithAspireWorldDisplay{TWorkerProject}"/> to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithServiceSwitches(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithServiceSwitches() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_SWITCHES", "true");
        return builder;
    }

    /// <summary>
    /// Enables debug-level logging for RCON commands sent to the Minecraft server.
    /// When enabled, every RCON command and its response are logged to the Aspire dashboard.
    /// Useful for troubleshooting world building issues or verifying command execution.
    /// Requires <see cref="WithAspireWorldDisplay{TWorkerProject}"/> to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithRconDebugLogging(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithRconDebugLogging() requires WithAspireWorldDisplay() to be called first.");

        // Set log level to Debug for RconService specifically
        workerBuilder.WithEnvironment("Logging__LogLevel__Aspire.Hosting.Minecraft.Worker.Services.RconService", "Debug");
        return builder;
    }

    /// <summary>
    /// Enables peaceful mode — immediately removes all hostile mobs (zombies, skeletons, creepers, etc.)
    /// and prevents them from spawning. Passive mobs (cows, pigs, sheep) continue to spawn normally.
    /// Uses the Minecraft <c>/difficulty peaceful</c> command at server startup.
    /// Requires <see cref="WithAspireWorldDisplay{TWorkerProject}"/> to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithPeacefulMode(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithPeacefulMode() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_PEACEFUL", "true");
        return builder;
    }

    /// <summary>
    /// Enables a Redstone Dashboard wall west of the village that displays real-time health history using redstone lamps.
    /// Requires <see cref="WithAspireWorldDisplay{TWorkerProject}"/> to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithRedstoneDashboard(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithRedstoneDashboard() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_REDSTONE_DASHBOARD", "true");
        return builder;
    }

    /// <summary>
    /// Enables the minecart rail network connecting dependent resources.
    /// Powered rails with automated chest minecarts run between buildings
    /// that have dependency relationships. Complementary to redstone wiring.
    /// Requires <see cref="WithAspireWorldDisplay{TWorkerProject}"/> to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithMinecartRails(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithMinecartRails() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_MINECART_RAILS", "true");
        return builder;
    }

    /// <summary>
    /// Enables the error boat visualization system.
    /// When a resource becomes unhealthy, a boat carrying a creeper spawns at its canal entrance
    /// and floats toward the shared lake, providing a visual error indicator.
    /// Requires <see cref="WithAspireWorldDisplay{TWorkerProject}"/> to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithErrorBoats(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithErrorBoats() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_ERROR_BOATS", "true");
        return builder;
    }

    /// <summary>
    /// Enables water canal network connecting buildings to a shared lake.
    /// Requires <see cref="WithAspireWorldDisplay{TWorkerProject}"/> to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithCanals(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithCanals() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_CANALS", "true");
        return builder;
    }

    /// <summary>
    /// Enables neighborhood-based layout grouping. Resources of the same type (Azure, .NET, containers,
    /// executables) are grouped into distinct zones within the village, creating neighborhood clusters.
    /// Groups of 4+ resources of the same type will eventually form town squares with fountains (Phase 2).
    /// Requires <see cref="WithAspireWorldDisplay{TWorker}"/> to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithNeighborhoods(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        var workerBuilder = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithNeighborhoods() requires WithAspireWorldDisplay() to be called first.");

        workerBuilder.WithEnvironment("ASPIRE_FEATURE_NEIGHBORHOODS", "true");
        return builder;
    }

    /// <summary>
    /// Enables all opt-in Minecraft world display features at once.
    /// This is a convenience method equivalent to calling:
    /// <see cref="WithParticleEffects"/>, <see cref="WithTitleAlerts"/>, <see cref="WithWeatherEffects"/>,
    /// <see cref="WithBossBar"/>, <see cref="WithSoundEffects"/>, <see cref="WithActionBarTicker"/>,
    /// <see cref="WithBeaconTowers"/>, <see cref="WithFireworks"/>, <see cref="WithGuardianMobs"/>,
    /// <see cref="WithDeploymentFanfare"/>, <see cref="WithWorldBorderPulse"/>, <see cref="WithAchievements"/>,
    /// <see cref="WithHeartbeat"/>, <see cref="WithServiceSwitches"/>,
    /// <see cref="WithPeacefulMode"/>, <see cref="WithRedstoneDashboard"/>, <see cref="WithRconDebugLogging"/>,
    /// <see cref="WithMinecartRails"/>, <see cref="WithErrorBoats"/>, <see cref="WithCanals"/>, and <see cref="WithNeighborhoods"/>.
    /// Requires <see cref="WithAspireWorldDisplay{TWorkerProject}"/> to be called first.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithAspireWorldDisplay() has not been called first.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithAllFeatures(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        _ = builder.Resource.WorkerBuilder
            ?? throw new InvalidOperationException(
                "WithAllFeatures() requires WithAspireWorldDisplay() to be called first.");

        return builder
            .WithParticleEffects()
            .WithTitleAlerts()
            .WithWeatherEffects()
            .WithBossBar()
            .WithSoundEffects()
            .WithActionBarTicker()
            .WithBeaconTowers()
            .WithFireworks()
            .WithGuardianMobs()
            .WithDeploymentFanfare()
            .WithWorldBorderPulse()
            .WithAchievements()
            .WithHeartbeat()
            .WithServiceSwitches()
            .WithPeacefulMode()
            .WithRedstoneDashboard()
            .WithRconDebugLogging()
            .WithMinecartRails()
            .WithErrorBoats()
            .WithCanals()
            .WithNeighborhoods();
    }

    /// <summary>
    /// Sets an arbitrary Minecraft <c>server.properties</c> value via the itzg/minecraft-server
    /// environment variable convention. The property name is converted to UPPER_SNAKE_CASE
    /// (e.g., <c>max-players</c> becomes <c>MAX_PLAYERS</c>).
    /// Properties set here override any defaults configured by <see cref="AddMinecraftServer"/>.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="propertyName">The <c>server.properties</c> property name (e.g., <c>"max-players"</c>, <c>"difficulty"</c>).</param>
    /// <param name="value">The value to set for the property.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithServerProperty(
        this IResourceBuilder<MinecraftServerResource> builder,
        string propertyName,
        string value)
    {
        var envVarName = ConvertPropertyNameToEnvVar(propertyName);
        return builder.WithEnvironment(envVarName, value);
    }

    /// <summary>
    /// Sets a Minecraft <c>server.properties</c> value using a well-known <see cref="ServerProperty"/> key.
    /// The enum member is automatically converted to the itzg/minecraft-server UPPER_SNAKE_CASE env var name.
    /// Properties set here override any defaults configured by <see cref="AddMinecraftServer"/>.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="property">The server property to set.</param>
    /// <param name="value">The value to set for the property.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithServerProperty(
        this IResourceBuilder<MinecraftServerResource> builder,
        ServerProperty property,
        string value)
    {
        var envVarName = ConvertEnumToEnvVar(property.ToString());
        return builder.WithEnvironment(envVarName, value);
    }

    /// <summary>
    /// Sets multiple Minecraft <c>server.properties</c> values at once via the itzg/minecraft-server
    /// environment variable convention. Each property name is converted to UPPER_SNAKE_CASE.
    /// Properties set here override any defaults configured by <see cref="AddMinecraftServer"/>.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="properties">A dictionary of <c>server.properties</c> names and their values.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithServerProperties(
        this IResourceBuilder<MinecraftServerResource> builder,
        Dictionary<string, string> properties)
    {
        foreach (var (propertyName, value) in properties)
        {
            builder = builder.WithServerProperty(propertyName, value);
        }
        return builder;
    }

    /// <summary>
    /// Sets the game mode for the Minecraft server.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="mode">The game mode: <c>"survival"</c>, <c>"creative"</c>, <c>"adventure"</c>, or <c>"spectator"</c>.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithGameMode(
        this IResourceBuilder<MinecraftServerResource> builder,
        string mode)
    {
        return builder.WithEnvironment("MODE", mode);
    }

    /// <summary>
    /// Sets the game mode for the Minecraft server using a strongly-typed <see cref="MinecraftGameMode"/> value.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="mode">The game mode.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithGameMode(
        this IResourceBuilder<MinecraftServerResource> builder,
        MinecraftGameMode mode)
    {
        return builder.WithEnvironment("MODE", mode.ToString().ToLowerInvariant());
    }

    /// <summary>
    /// Sets the difficulty level for the Minecraft server.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="difficulty">The difficulty: <c>"peaceful"</c>, <c>"easy"</c>, <c>"normal"</c>, or <c>"hard"</c>.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithDifficulty(
        this IResourceBuilder<MinecraftServerResource> builder,
        string difficulty)
    {
        return builder.WithEnvironment("DIFFICULTY", difficulty);
    }

    /// <summary>
    /// Sets the difficulty level for the Minecraft server using a strongly-typed <see cref="MinecraftDifficulty"/> value.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="difficulty">The difficulty level.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithDifficulty(
        this IResourceBuilder<MinecraftServerResource> builder,
        MinecraftDifficulty difficulty)
    {
        return builder.WithEnvironment("DIFFICULTY", difficulty.ToString().ToLowerInvariant());
    }

    /// <summary>
    /// Sets the maximum number of players allowed on the Minecraft server.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="maxPlayers">The maximum number of players.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithMaxPlayers(
        this IResourceBuilder<MinecraftServerResource> builder,
        int maxPlayers)
    {
        return builder.WithEnvironment("MAX_PLAYERS", maxPlayers.ToString());
    }

    /// <summary>
    /// Sets the message of the day (MOTD) shown in the Minecraft server browser.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="motd">The message of the day text.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithMotd(
        this IResourceBuilder<MinecraftServerResource> builder,
        string motd)
    {
        return builder.WithEnvironment("MOTD", motd);
    }

    /// <summary>
    /// Sets the world seed for the Minecraft server.
    /// Overrides the default seed configured by <see cref="AddMinecraftServer"/>.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="seed">The world generation seed.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithWorldSeed(
        this IResourceBuilder<MinecraftServerResource> builder,
        string seed)
    {
        return builder.WithEnvironment("SEED", seed);
    }

    /// <summary>
    /// Enables or disables player-versus-player combat on the Minecraft server.
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="enabled"><c>true</c> to enable PvP (default); <c>false</c> to disable it.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<MinecraftServerResource> WithPvp(
        this IResourceBuilder<MinecraftServerResource> builder,
        bool enabled = true)
    {
        return builder.WithEnvironment("PVP", enabled ? "true" : "false");
    }

    /// <summary>
    /// Loads Minecraft <c>server.properties</c> from a file and applies each property via
    /// <see cref="WithServerProperty(IResourceBuilder{MinecraftServerResource}, string, string)"/>.
    /// The file is read at build/configuration time — values become environment variables on the container.
    /// Properties loaded from the file can be overridden by subsequent <c>WithServerProperty()</c> calls.
    /// <para>
    /// Expected file format (standard Minecraft <c>server.properties</c>):
    /// <code>
    /// # Lines starting with # are comments
    /// max-players=20
    /// motd=My Minecraft Server
    /// difficulty=normal
    /// pvp=true
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="builder">The Minecraft server resource builder.</param>
    /// <param name="filePath">
    /// Path to a <c>server.properties</c> file. Relative paths are resolved from the AppHost project directory.
    /// </param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    public static IResourceBuilder<MinecraftServerResource> WithServerPropertiesFile(
        this IResourceBuilder<MinecraftServerResource> builder,
        string filePath)
    {
        var resolvedPath = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(builder.ApplicationBuilder.AppHostDirectory, filePath);

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException(
                $"The server.properties file was not found: {resolvedPath}", resolvedPath);
        }

        foreach (var line in File.ReadLines(resolvedPath))
        {
            // Skip blank lines and comments
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            // Split only on the first '=' to handle values containing '='
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            builder = builder.WithServerProperty(key, value);
        }

        return builder;
    }

    /// <summary>
    /// Converts a Minecraft <c>server.properties</c> property name to the itzg/minecraft-server
    /// environment variable convention: uppercase with hyphens replaced by underscores.
    /// </summary>
    private static string ConvertPropertyNameToEnvVar(string propertyName)
    {
        return propertyName.ToUpperInvariant().Replace('-', '_');
    }

    /// <summary>
    /// Converts a PascalCase enum member name to UPPER_SNAKE_CASE for the itzg/minecraft-server
    /// environment variable convention (e.g., <c>MaxPlayers</c> → <c>MAX_PLAYERS</c>).
    /// </summary>
    private static string ConvertEnumToEnvVar(string pascalName)
    {
        var result = new System.Text.StringBuilder(pascalName.Length + 4);
        for (var i = 0; i < pascalName.Length; i++)
        {
            var c = pascalName[i];
            if (char.IsUpper(c) && i > 0)
            {
                result.Append('_');
            }
            result.Append(char.ToUpperInvariant(c));
        }
        return result.ToString();
    }
}

/// <summary>
/// Annotation indicating a Modrinth plugin should be installed.
/// </summary>
internal class ModrinthPluginAnnotation(string slug) : IResourceAnnotation
{
    public string Slug => slug;
}

/// <summary>
/// Annotation indicating the Aspire world display worker should connect to this server.
/// </summary>
internal class AspireWorldDisplayAnnotation : IResourceAnnotation;

/// <summary>
/// Annotation indicating this server uses a pre-baked Docker image with plugins already installed.
/// Methods like <see cref="MinecraftServerBuilderExtensions.WithBlueMap"/> check for this to skip
/// redundant bind-mounts.
/// </summary>
internal class PrebakedImageAnnotation : IResourceAnnotation;
