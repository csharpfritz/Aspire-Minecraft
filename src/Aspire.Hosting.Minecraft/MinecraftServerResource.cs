using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Minecraft;

/// <summary>
/// Represents a Minecraft server container resource managed by Aspire.
/// </summary>
public class MinecraftServerResource : ContainerResource, IResourceWithConnectionString
{
    internal const string GameEndpointName = "game";
    internal const string RconEndpointName = "rcon";
    internal const string BlueMapEndpointName = "bluemap";

    public MinecraftServerResource(string name) : base(name)
    {
    }

    /// <summary>
    /// The RCON password used to authenticate with the server.
    /// </summary>
    public ParameterResource? RconPasswordParameter { get; set; }

    /// <summary>
    /// The internally-managed worker project builder, created by WithAspireWorldDisplay().
    /// WithMonitoredResource() applies env vars to this builder.
    /// </summary>
    internal IResourceBuilder<ProjectResource>? WorkerBuilder { get; set; }

    /// <summary>
    /// Gets the connection string expression for RCON access.
    /// Uses the RCON endpoint's host and port for proper resolution.
    /// Format: Host=host;Port=port;Password=password
    /// </summary>
    public ReferenceExpression ConnectionStringExpression
    {
        get
        {
            var rconEndpoint = new EndpointReference(this, RconEndpointName);
            return ReferenceExpression.Create(
                $"Host={rconEndpoint.Property(EndpointProperty.Host)};Port={rconEndpoint.Property(EndpointProperty.Port)};Password={RconPasswordParameter}");
        }
    }
}
