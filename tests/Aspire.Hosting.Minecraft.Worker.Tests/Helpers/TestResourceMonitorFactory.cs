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
        // Access the private _resources field via reflection
        var field = typeof(AspireResourceMonitor)
            .GetField("_resources", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (Dictionary<string, ResourceInfo>)field.GetValue(monitor)!;

        dict.Clear();
        foreach (var (name, healthy) in resources)
        {
            var status = healthy ? ResourceStatus.Healthy : ResourceStatus.Unhealthy;
            dict[name] = new ResourceInfo(name, "Project", "", "", 0, status);
        }
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
