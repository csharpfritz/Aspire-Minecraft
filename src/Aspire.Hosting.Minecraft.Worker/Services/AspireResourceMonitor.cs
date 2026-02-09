using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Tracks the health status of sibling Aspire resources.
/// Discovers resources via environment variables and polls HTTP endpoints.
/// </summary>
public sealed class AspireResourceMonitor
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
    /// Convention: ASPIRE_RESOURCE_{NAME}_URL and ASPIRE_RESOURCE_{NAME}_TYPE
    /// </summary>
    public void DiscoverResources()
    {
        var envVars = Environment.GetEnvironmentVariables();
        foreach (System.Collections.DictionaryEntry entry in envVars)
        {
            var key = entry.Key?.ToString() ?? "";
            if (key.StartsWith("ASPIRE_RESOURCE_") && key.EndsWith("_URL"))
            {
                var name = key["ASPIRE_RESOURCE_".Length..^"_URL".Length].ToLowerInvariant();
                var url = entry.Value?.ToString() ?? "";
                var typeKey = $"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_TYPE";
                var type = Environment.GetEnvironmentVariable(typeKey) ?? "Unknown";

                if (!string.IsNullOrEmpty(url))
                {
                    _resources[name] = new ResourceInfo(name, type, url, ResourceStatus.Unknown);
                    _logger.LogInformation("Discovered Aspire resource: {ResourceName} ({ResourceType}) at {Url}",
                        name, type, url);
                }
            }
        }
    }

    /// <summary>
    /// Polls all known resources and updates their health status.
    /// Returns resources whose status changed.
    /// </summary>
    public async Task<List<ResourceStatusChange>> PollHealthAsync(CancellationToken ct = default)
    {
        var changes = new List<ResourceStatusChange>();

        foreach (var (name, info) in _resources)
        {
            var oldStatus = info.Status;
            var newStatus = await CheckHealthAsync(info.Url, ct);

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

    private async Task<ResourceStatus> CheckHealthAsync(string url, CancellationToken ct)
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

    public int HealthyCount => _resources.Values.Count(r => r.Status == ResourceStatus.Healthy);
    public int TotalCount => _resources.Count;
}

public record ResourceInfo(string Name, string Type, string Url, ResourceStatus Status);

public record ResourceStatusChange(string Name, string Type, ResourceStatus OldStatus, ResourceStatus NewStatus);

public enum ResourceStatus
{
    Unknown,
    Healthy,
    Unhealthy
}
