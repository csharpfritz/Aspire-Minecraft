using System.Reflection;
using Aspire.Hosting.Minecraft.Worker.Services;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Minecraft.Worker.Tests.Helpers;

/// <summary>
/// Creates and configures <see cref="AspireResourceMonitor"/> instances for testing.
/// Uses reflection to inject resources directly, bypassing HTTP/TCP health checks.
/// </summary>
internal static class TestResourceMonitorFactory
{
    /// <summary>
    /// Creates an empty <see cref="AspireResourceMonitor"/> suitable for tests.
    /// </summary>
    public static AspireResourceMonitor Create()
    {
        var httpFactory = new FakeHttpClientFactory();
        return new AspireResourceMonitor(NullLogger<AspireResourceMonitor>.Instance, httpFactory);
    }

    /// <summary>
    /// Sets the internal resource dictionary to a known set of resources with specified health states.
    /// </summary>
    public static void SetResources(AspireResourceMonitor monitor, params (string name, bool healthy)[] resources)
    {
        var typed = resources.Select(r => (r.name, "Project", r.healthy ? ResourceStatus.Healthy : ResourceStatus.Unhealthy)).ToArray();
        SetResourcesWithTypes(monitor, typed);
    }

    /// <summary>
    /// Sets resources with explicit type and status for testing beacon colors, structure types, etc.
    /// </summary>
    public static void SetResourcesWithTypes(AspireResourceMonitor monitor, params (string name, string type, ResourceStatus status)[] resources)
    {
        var field = typeof(AspireResourceMonitor)
            .GetField("_resources", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (Dictionary<string, ResourceInfo>)field.GetValue(monitor)!;

        dict.Clear();
        foreach (var (name, type, status) in resources)
        {
            dict[name] = new ResourceInfo(name, type, "", "", 0, "", status);
        }
    }

    /// <summary>
    /// Sets resources with explicit type, status, and dependencies for testing dependency-aware services.
    /// </summary>
    public static void SetResourcesWithDependencies(AspireResourceMonitor monitor, params (string name, string type, ResourceStatus status, string[] dependencies)[] resources)
    {
        var field = typeof(AspireResourceMonitor)
            .GetField("_resources", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (Dictionary<string, ResourceInfo>)field.GetValue(monitor)!;

        dict.Clear();
        foreach (var (name, type, status, dependencies) in resources)
        {
            dict[name] = new ResourceInfo(name, type, "", "", 0, "", status, dependencies);
        }
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
