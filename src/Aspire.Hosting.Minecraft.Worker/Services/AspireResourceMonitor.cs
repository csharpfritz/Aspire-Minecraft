using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Sockets;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Tracks the health status of sibling Aspire resources.
/// Discovers resources via environment variables and polls HTTP endpoints or TCP sockets.
/// </summary>
internal sealed class AspireResourceMonitor
{
    private readonly ILogger<AspireResourceMonitor> _logger;
    private readonly HttpClient _http;
    private readonly Dictionary<string, ResourceInfo> _resources = new();

    public AspireResourceMonitor(ILogger<AspireResourceMonitor> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _http = httpClientFactory.CreateClient("aspire-monitor");
        _http.Timeout = TimeSpan.FromSeconds(5);
    }

    public IReadOnlyDictionary<string, ResourceInfo> Resources => _resources;

    /// <summary>
    /// Discovers resources from environment variables.
    /// Convention: ASPIRE_RESOURCE_{NAME}_TYPE, optionally _URL (HTTP) or _HOST/_PORT (TCP).
    /// </summary>
    public void DiscoverResources()
    {
        var envVars = Environment.GetEnvironmentVariables();

        foreach (System.Collections.DictionaryEntry entry in envVars)
        {
            var key = entry.Key?.ToString() ?? "";
            if (key.StartsWith("ASPIRE_RESOURCE_") && key.EndsWith("_TYPE"))
            {
                var name = key["ASPIRE_RESOURCE_".Length..^"_TYPE".Length].ToLowerInvariant();
                var type = entry.Value?.ToString() ?? "Unknown";
                var upperName = name.ToUpperInvariant();
                var url = Environment.GetEnvironmentVariable($"ASPIRE_RESOURCE_{upperName}_URL") ?? "";
                var host = Environment.GetEnvironmentVariable($"ASPIRE_RESOURCE_{upperName}_HOST") ?? "";
                var portStr = Environment.GetEnvironmentVariable($"ASPIRE_RESOURCE_{upperName}_PORT") ?? "";
                int.TryParse(portStr, out var port);

                _resources[name] = new ResourceInfo(name, type, url, host, port, ResourceStatus.Unknown);

                var endpoint = !string.IsNullOrEmpty(url) ? url
                    : !string.IsNullOrEmpty(host) ? $"{host}:{port} (tcp)"
                    : "(no endpoint)";
                _logger.LogInformation("Discovered Aspire resource: {ResourceName} ({ResourceType}) at {Endpoint}",
                    name, type, endpoint);
            }
        }
    }

    /// <summary>
    /// Polls all known resources and updates their health status.
    /// Uses HTTP for resources with a URL, TCP socket for resources with host:port, 
    /// and assumes healthy for resources with no endpoint.
    /// </summary>
    public async Task<List<ResourceStatusChange>> PollHealthAsync(CancellationToken ct = default)
    {
        var changes = new List<ResourceStatusChange>();

        foreach (var (name, info) in _resources)
        {
            var oldStatus = info.Status;
            ResourceStatus newStatus;

            if (!string.IsNullOrEmpty(info.Url))
            {
                newStatus = await CheckHttpHealthAsync(info.Url, ct);
            }
            else if (!string.IsNullOrEmpty(info.TcpHost) && info.TcpPort > 0)
            {
                newStatus = await CheckTcpHealthAsync(info.TcpHost, info.TcpPort, ct);
            }
            else
            {
                newStatus = ResourceStatus.Healthy;
            }

            if (newStatus != oldStatus)
            {
                _resources[name] = info with { Status = newStatus };
                changes.Add(new ResourceStatusChange(name, info.Type, oldStatus, newStatus));
                _logger.LogInformation("Resource health changed: {ResourceName} {OldStatus} -> {NewStatus}",
                    name, oldStatus, newStatus);
            }
        }

        return changes;
    }

    private async Task<ResourceStatus> CheckHttpHealthAsync(string url, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync(url, ct);
            return response.IsSuccessStatusCode ? ResourceStatus.Healthy : ResourceStatus.Unhealthy;
        }
        catch
        {
            return ResourceStatus.Unhealthy;
        }
    }

    private static async Task<ResourceStatus> CheckTcpHealthAsync(string host, int port, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(host, port, cts.Token);
            return ResourceStatus.Healthy;
        }
        catch
        {
            return ResourceStatus.Unhealthy;
        }
    }

    public int HealthyCount => _resources.Values.Count(r => r.Status == ResourceStatus.Healthy);
    public int TotalCount => _resources.Count;
}

internal record ResourceInfo(string Name, string Type, string Url, string TcpHost, int TcpPort, ResourceStatus Status);

internal record ResourceStatusChange(string Name, string Type, ResourceStatus OldStatus, ResourceStatus NewStatus);

internal enum ResourceStatus
{
    Unknown,
    Healthy,
    Unhealthy
}
