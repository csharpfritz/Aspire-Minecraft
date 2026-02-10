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
    /// Includes a built-in RCON health check so dependents can use WaitFor().
    /// </summary>
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
            .WithEnvironment("SEED", "aspire2026")
            .WithEnvironment("ENABLE_RCON", "true")
            .WithEnvironment("RCON_PORT", "25575")
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["RCON_PASSWORD"] = rconPassword.Resource;
            })
            .WithVolume($"{name}-data", "/data")
            .WithHealthCheck(healthCheckKey);
    }

    /// <summary>
    /// Adds BlueMap web map plugin, exposing its web UI as an HTTP endpoint in the Aspire dashboard.
    /// </summary>
    public static IResourceBuilder<MinecraftServerResource> WithBlueMap(
        this IResourceBuilder<MinecraftServerResource> builder,
        int? port = null)
    {
        // Resolve the bundled core.conf that has accept-download: true
        var assemblyDir = Path.GetDirectoryName(typeof(MinecraftServerBuilderExtensions).Assembly.Location)!;
        var coreConfPath = Path.Combine(assemblyDir, "bluemap", "core.conf");

        var result = builder
            .WithEndpoint(port: port, targetPort: 8100, name: MinecraftServerResource.BlueMapEndpointName, scheme: "http")
            .WithAnnotation(new ModrinthPluginAnnotation("bluemap"))
            .WithEnvironment(context =>
            {
                AppendModrinthProject(context, "bluemap");
            });

        // Bind-mount the core.conf into /plugins/BlueMap/ so itzg copies it to
        // /data/plugins/BlueMap/core.conf before the server starts.
        // This ensures accept-download: true is set on first boot.
        if (File.Exists(coreConfPath))
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
    /// Registers the bundled worker service that connects via RCON to render Aspire state in Minecraft.
    /// </summary>
    public static IResourceBuilder<MinecraftServerResource> WithAspireWorldDisplay(
        this IResourceBuilder<MinecraftServerResource> builder)
    {
        // Add DecentHolograms plugin (required for holograms)
        builder = builder.WithDecentHolograms();

        // The worker service will be added as a project reference by the AppHost.
        // We annotate the resource so the worker knows to connect.
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
    /// </summary>
    public static IResourceBuilder<T> WithMonitoredResource<T>(
        this IResourceBuilder<T> workerBuilder,
        IResourceBuilder<IResourceWithEndpoints> resource) where T : IResourceWithEnvironment
    {
        var name = resource.Resource.Name;

        // Determine resource type from the concrete type
        var resourceType = resource.Resource switch
        {
            ProjectResource => "Project",
            ContainerResource => "Container",
            _ => resource.Resource.GetType().Name.Replace("Resource", "")
        };

        workerBuilder.WithEnvironment($"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_TYPE", resourceType);

        // Try to get an HTTP endpoint URL for health checking.
        // GetEndpoint() never throws — it creates a dangling reference for missing endpoints.
        // We must check annotations directly to see if the endpoint actually exists.
        workerBuilder.WithEnvironment(context =>
        {
            if (resource.Resource is IResourceWithEndpoints resourceWithEndpoints)
            {
                EndpointReference? endpointRef = null;
                foreach (var ep in resourceWithEndpoints.GetEndpoints())
                {
                    if (string.Equals(ep.EndpointName, "http", StringComparison.OrdinalIgnoreCase))
                    {
                        endpointRef = ep;
                        break;
                    }
                    if (string.Equals(ep.EndpointName, "https", StringComparison.OrdinalIgnoreCase))
                    {
                        endpointRef = ep;
                    }
                }

                if (endpointRef is not null)
                {
                    context.EnvironmentVariables[$"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_URL"] =
                        endpointRef.Property(EndpointProperty.Url);
                }
            }
        });

        return workerBuilder;
    }

    /// <summary>
    /// Adds an Aspire resource to be monitored (for resources without endpoints).
    /// The resource will appear in the Minecraft world but health won't be polled.
    /// </summary>
    public static IResourceBuilder<T> WithMonitoredResource<T>(
        this IResourceBuilder<T> workerBuilder,
        IResourceBuilder<IResource> resource,
        string resourceType) where T : IResourceWithEnvironment
    {
        var name = resource.Resource.Name;
        workerBuilder.WithEnvironment($"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_TYPE", resourceType);
        return workerBuilder;
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
