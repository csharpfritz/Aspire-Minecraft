# Azure SDK Research — Technical Feasibility for Azure Resource Group → Minecraft

> **Status:** Research document — no implementation  
> **Author:** Shuri (Backend Dev)  
> **Date:** 2026-02-10  
> **Requested by:** Jeffrey T. Fritz

---

## 1. Azure SDK Packages Needed

### Core Packages

| Package | Stable Version | .NET Targets | Purpose | Dependencies |
|---------|---------------|--------------|---------|-------------|
| **Azure.ResourceManager** | 1.13.2 | .NET Standard 2.0, .NET 8.0+ | ARM client, resource group access, generic resource enumeration | Azure.Core, System.Text.Json, System.ClientModel |
| **Azure.Identity** | 1.17.1 | .NET Standard 2.0, .NET 8.0+ | Authentication (DefaultAzureCredential and friends) | Azure.Core, MSAL, System.Security.Cryptography |
| **Azure.ResourceManager.ResourceHealth** | 1.0.0 | .NET Standard 2.0 | Resource health/availability status queries | Azure.ResourceManager (core) |
| **Azure.Monitor.Query.Metrics** | 1.0.0 | .NET Standard 2.0, .NET 8.0+ | Metrics queries (CPU, memory, etc.) — **replaces deprecated `Azure.Monitor.Query`** | Azure.Core |

### Per-Resource-Type Packages (Optional, for typed access)

These are only needed if we want strongly-typed resource access beyond `GenericResource`:

| Package | Example |
|---------|---------|
| `Azure.ResourceManager.Compute` | VM details, availability sets |
| `Azure.ResourceManager.AppService` | Web Apps, App Service Plans |
| `Azure.ResourceManager.Sql` | Azure SQL databases |
| `Azure.ResourceManager.Storage` | Storage accounts |
| `Azure.ResourceManager.ContainerService` | AKS clusters |

> **Recommendation:** Start with `GenericResource` enumeration (no per-type packages needed). Add typed packages only when we need resource-type-specific properties or actions.

### Deprecated — Do Not Use

| Package | Version | Status |
|---------|---------|--------|
| `Azure.Monitor.Query` | 1.7.1 | **Deprecated Oct 2025** — replaced by `Azure.Monitor.Query.Metrics` and `Azure.Monitor.Query.Logs` |

### Package Size Impact

The Azure SDK has a significant dependency graph. Core packages alone pull in:
- `Azure.Core` (~500 KB) — HTTP pipeline, retry policies, diagnostics
- `Microsoft.Identity.Client` (MSAL) (~1.5 MB) — OAuth2/OIDC flows
- `System.ClientModel` — base model types
- `System.Text.Json` — JSON serialization

**Estimated total added dependency weight: ~5–8 MB** (compressed NuGet, ~15–20 MB on disk), depending on which packages are included and whether trimming is used.

---

## 2. Resource Discovery API

### Listing All Resources in a Resource Group

The `ResourceGroupResource` does not have a direct `GetGenericResources()` method. Instead, use `SubscriptionResource.GetGenericResourcesAsync()` with an OData `$filter` to scope to a resource group, or use the ARM REST API scoped to the resource group.

**Approach 1: Filter at subscription level (most practical)**

```csharp
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;

var client = new ArmClient(new DefaultAzureCredential());
var subscription = await client.GetDefaultSubscriptionAsync();

// List all resources in a specific resource group
var resourceGroup = await subscription.GetResourceGroupAsync("my-resource-group");
ResourceGroupResource rg = resourceGroup.Value;

// Use the GetGenericResourcesAsync with resource group filter
await foreach (GenericResource resource in subscription.GetGenericResourcesAsync(
    filter: $"resourceGroup eq '{rg.Data.Name}'",
    expand: "createdTime,changedTime"))
{
    Console.WriteLine($"Name: {resource.Data.Name}");
    Console.WriteLine($"Type: {resource.Data.ResourceType}");
    Console.WriteLine($"Location: {resource.Data.Location}");
    Console.WriteLine($"Provisioning State: {resource.Data.ProvisioningState}");
    Console.WriteLine($"Tags: {string.Join(", ", resource.Data.Tags ?? new Dictionary<string, string>())}");
}
```

**Approach 2: Use typed resource collections from the resource group**

```csharp
// If you know the resource types, you can enumerate typed collections
var rg = (await subscription.GetResourceGroupAsync("my-resource-group")).Value;

// Example: list all storage accounts in the resource group
// (requires Azure.ResourceManager.Storage)
await foreach (var storageAccount in rg.GetStorageAccounts())
{
    Console.WriteLine(storageAccount.Data.Name);
}
```

### Data Returned per Resource

Each `GenericResource` (or `GenericResourceData`) includes:

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `ResourceIdentifier` | Full ARM resource ID |
| `Name` | `string` | Resource name |
| `ResourceType` | `ResourceType` | e.g., `Microsoft.Compute/virtualMachines` |
| `Location` | `AzureLocation` | Azure region |
| `ProvisioningState` | `string` | `Succeeded`, `Failed`, `Creating`, `Deleting`, etc. |
| `Tags` | `IDictionary<string, string>` | User-defined tags |
| `CreatedOn` | `DateTimeOffset?` | Creation time (requires `expand: "createdTime"`) |
| `ChangedOn` | `DateTimeOffset?` | Last change time (requires `expand: "changedTime"`) |

### Filtering by Resource Type

Use OData `$filter` syntax:

```csharp
// Filter to only VMs and web apps
await foreach (var res in subscription.GetGenericResourcesAsync(
    filter: "resourceType eq 'Microsoft.Compute/virtualMachines' " +
            "or resourceType eq 'Microsoft.Web/sites'"))
{
    // ...
}
```

### Pagination

The SDK handles pagination automatically via `AsyncPageable<T>`. Internally, it fetches pages of results (typically 100 per page) and yields them one at a time. No manual pagination code needed — just use `await foreach`.

### Rate Limits

ARM uses a **token bucket** throttling model per subscription per region:

| Operation | Bucket Size | Refill Rate |
|-----------|-------------|-------------|
| **Reads** | 250 | 25/sec |
| **Writes** | 200 | 10/sec |
| **Deletes** | 200 | 10/sec |

If throttled, the API returns HTTP 429 with a `Retry-After` header. The Azure SDK's built-in retry policy handles this automatically.

**For our use case** (listing ~10–50 resources in a single resource group every 30–60 seconds), we are well within limits. A single list call is one read request, with pagination adding one request per ~100 resources.

---

## 3. Resource Health Monitoring

### Option A: Azure Resource Health API (`Azure.ResourceManager.ResourceHealth`)

**Health States Available:**

| State | Meaning | Map to Our Model |
|-------|---------|------------------|
| `Available` | Resource is healthy and operating normally | `ResourceStatus.Healthy` |
| `Degraded` | Resource experiencing performance/connectivity issues | `ResourceStatus.Unhealthy` (or new `Degraded` if we add it) |
| `Unavailable` | Resource is down or unreachable | `ResourceStatus.Unhealthy` |
| `Unknown` | Azure can't determine health | `ResourceStatus.Unknown` |

**Code Example:**

```csharp
using Azure.ResourceManager.ResourceHealth;
using Azure.ResourceManager.ResourceHealth.Models;

// Get health status for a specific resource
var resourceId = new ResourceIdentifier(
    "/subscriptions/{subId}/resourceGroups/{rgName}/providers/Microsoft.Compute/virtualMachines/{vmName}");

var genericResource = client.GetGenericResource(resourceId);
// The Resource Health extension methods would be used here
// to get availability statuses

// Via REST: GET {resourceId}/providers/Microsoft.ResourceHealth/availabilityStatuses/current
```

> **⚠️ Needs verification:** The `Azure.ResourceManager.ResourceHealth` v1.0.0 SDK's exact method for getting per-resource health status needs hands-on testing. The REST API is `GET {resourceId}/providers/Microsoft.ResourceHealth/availabilityStatuses/current`, but the SDK wrapper may require specific extension method calls.

**Freshness:** Resource Health data is typically updated every 1–5 minutes. Not real-time.  
**Rate Limits:** Same ARM rate limits apply (reads bucket: 250 / 25 per sec).

### Option B: Azure Monitor Metrics (`Azure.Monitor.Query.Metrics`)

Provides quantitative health signals (CPU %, memory, request count, error rate).

```csharp
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;

var metricsClient = new MetricsClient(
    new Uri("https://eastus.metrics.monitor.azure.com"),
    new DefaultAzureCredential());

// Query CPU percentage for a VM
var result = await metricsClient.QueryResourceAsync(
    resourceId,
    new[] { "Percentage CPU" },
    new MetricsQueryOptions
    {
        TimeRange = new QueryTimeRange(TimeSpan.FromMinutes(5)),
        Granularity = TimeSpan.FromMinutes(1)
    });
```

**Freshness:** Metrics are available within 1–3 minutes of collection.  
**Rate Limits:** Monitor API has separate throttling — generally 12,000 requests per hour per subscription.  
**Limitation:** Metrics vary by resource type. Not all resource types emit the same metrics. Would need per-type metric definitions.

### Option C: Provisioning State from ARM

The simplest approach — use the `ProvisioningState` from the resource list call.

```csharp
// Already available from the resource enumeration call
var provisioningState = resource.Data.ProvisioningState; // "Succeeded", "Failed", "Creating", etc.
```

| Provisioning State | Map to Our Model |
|-------------------|------------------|
| `Succeeded` | `ResourceStatus.Healthy` |
| `Failed` | `ResourceStatus.Unhealthy` |
| `Creating`, `Updating`, `Deleting` | `ResourceStatus.Unknown` (in transition) |

**Freshness:** Reflects the last ARM operation state. Does NOT detect runtime health issues (e.g., a VM with `Succeeded` provisioning but crashed OS).  
**Rate Limits:** No additional calls — comes free with resource list.

### Option D: Activity Log for State Changes

Azure Activity Log records control plane operations (resource created, deleted, updated, etc.).

**Freshness:** Near-real-time for control plane events.  
**Limitation:** Only captures ARM operations, not runtime health. Not useful for ongoing health monitoring.

### Recommended Approach for v1

**Use a layered approach:**

1. **Primary:** Provisioning state from ARM resource list (free, no extra calls)
2. **Secondary:** Resource Health API for resources that support it (adds real health data)
3. **Future (v2):** Azure Monitor metrics for quantitative health signals

**Health State Mapping:**

```csharp
internal static ResourceStatus MapAzureHealthToResourceStatus(
    string provisioningState,
    ResourceHealthAvailabilityStateValue? healthState)
{
    // Resource Health takes priority if available
    if (healthState.HasValue)
    {
        if (healthState.Value == ResourceHealthAvailabilityStateValue.Available)
            return ResourceStatus.Healthy;
        if (healthState.Value == ResourceHealthAvailabilityStateValue.Degraded)
            return ResourceStatus.Unhealthy; // Or add Degraded to our enum
        if (healthState.Value == ResourceHealthAvailabilityStateValue.Unavailable)
            return ResourceStatus.Unhealthy;
        return ResourceStatus.Unknown;
    }

    // Fall back to provisioning state
    return provisioningState switch
    {
        "Succeeded" => ResourceStatus.Healthy,
        "Failed" => ResourceStatus.Unhealthy,
        _ => ResourceStatus.Unknown
    };
}
```

---

## 4. Authentication Patterns

### DefaultAzureCredential Chain (for .NET)

`DefaultAzureCredential` tries credentials in this order:

| # | Credential | When It Works |
|---|-----------|---------------|
| 1 | `EnvironmentCredential` | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_CLIENT_SECRET` (or cert) env vars set |
| 2 | `WorkloadIdentityCredential` | Running in Kubernetes with workload identity federation |
| 3 | `ManagedIdentityCredential` | Running on an Azure host (VM, App Service, Container Apps) with managed identity |
| 4 | `SharedTokenCacheCredential` | Shared token from local tooling SSO |
| 5 | `VisualStudioCredential` | Signed into Visual Studio |
| 6 | `VisualStudioCodeCredential` | Signed into VS Code with Azure extension |
| 7 | `AzureCliCredential` | Signed in via `az login` |
| 8 | `AzurePowerShellCredential` | Signed in via `Connect-AzAccount` |
| 9 | `AzureDeveloperCliCredential` | Signed in via `azd auth login` |
| 10 | `InteractiveBrowserCredential` | (Only if explicitly enabled) |

### Local Development Setup

The simplest path for a developer:

```bash
# Install Azure CLI, then login
az login

# Optionally set a specific subscription
az account set --subscription "My Subscription"
```

That's it. `DefaultAzureCredential` will find the Azure CLI credentials automatically.

### CI/Production Setup

**Option 1: Service Principal (environment variables)**

```bash
# Set these environment variables in CI
export AZURE_TENANT_ID="your-tenant-id"
export AZURE_CLIENT_ID="your-client-id"
export AZURE_CLIENT_SECRET="your-client-secret"
```

**Option 2: Managed Identity (on Azure hosts)**

No configuration needed — `DefaultAzureCredential` detects the managed identity automatically.

### Code Example

```csharp
using Azure.Identity;
using Azure.ResourceManager;

// Works in all environments — local dev, CI, production
var client = new ArmClient(new DefaultAzureCredential());

// For faster local dev (skip irrelevant credential checks):
var client2 = new ArmClient(new DefaultAzureCredential(
    new DefaultAzureCredentialOptions
    {
        ExcludeSharedTokenCacheCredential = true,
        ExcludeVisualStudioCodeCredential = true
    }));
```

### Can We Use Connection Strings?

No. Azure Resource Manager uses OAuth2 bearer tokens, not connection strings. There's no way to bypass the token-based auth. The simplest path is always `DefaultAzureCredential` + `az login`.

### Configuration for Aspire

We'd configure via standard Aspire patterns — environment variables or configuration:

```csharp
// In AppHost
var minecraft = builder.AddMinecraftServer("mc")
    .WithAzureResourceGroup("my-subscription-id", "my-resource-group")
    // Subscription ID and RG name passed as env vars to worker
```

---

## 5. Polling vs Event-Driven

### Option 1: Polling ARM APIs

**How it works:** A `BackgroundService` calls `GetGenericResourcesAsync()` and optionally the Resource Health API on a timer.

| Aspect | Detail |
|--------|--------|
| **Polling interval** | 30–60 seconds is reasonable for a demo |
| **Requests per poll** | 1 list call + 1 Resource Health call per resource (N+1 pattern) |
| **Rate limit risk** | Negligible for <50 resources at 60s intervals (~50 reads/min vs 250 bucket + 25/sec refill) |
| **Latency** | Detects changes within one polling interval (30–60s) |
| **Complexity** | Low — straightforward `BackgroundService` with timer |

### Option 2: Azure Event Grid

**How it works:** Subscribe to resource state change events via Azure Event Grid system topics.

| Aspect | Detail |
|--------|--------|
| **Latency** | Near real-time push notifications |
| **Setup required** | Event Grid system topic, event subscription, webhook endpoint or Azure Function |
| **Packages** | `Azure.Messaging.EventGrid` (v5.0.0), plus Azure infrastructure setup |
| **Complexity** | **High** — requires additional Azure infrastructure (Event Grid topic, subscription), inbound webhook handling, authentication, dead-letter handling |
| **Rate limits** | Not ARM-rate-limited (push model), but Event Grid has its own throughput limits |

### Recommendation for v1: Polling

**Polling wins for v1** because:

1. **Simplicity** — No additional Azure infrastructure needed beyond a subscription and resource group
2. **Demo-friendly** — Works with `az login` locally, no Event Grid setup
3. **Sufficient latency** — 30–60 second polling is fine for a Minecraft visualization (world updates are already batched)
4. **Matches our existing pattern** — `AspireResourceMonitor` already polls on a timer
5. **No inbound networking** — Event Grid requires a reachable webhook endpoint; Minecraft servers typically don't expose one

**Consider Event Grid for v2** if:
- Users want sub-second resource state changes
- The feature moves to a hosted scenario (Azure Container Apps, etc.)
- We need to scale beyond a single resource group

---

## 6. Impact on Package Size

### Current Package Size

`Fritz.Aspire.Hosting.Minecraft` is currently ~39.6 MB (dominated by the 23 MB OpenTelemetry Java agent JAR).

### Azure SDK Dependency Weight

| Package | Approximate NuGet Size |
|---------|----------------------|
| `Azure.ResourceManager` | ~1.2 MB |
| `Azure.Identity` | ~400 KB (but pulls in MSAL ~1.5 MB) |
| `Azure.Core` | ~500 KB |
| `Azure.ResourceManager.ResourceHealth` | ~200 KB |
| `Azure.Monitor.Query.Metrics` | ~300 KB |
| `Microsoft.Identity.Client` (MSAL) | ~1.5 MB |
| Transitive deps (System.ClientModel, etc.) | ~500 KB |
| **Total added** | **~4.5–5 MB** |

### Should This Be a Separate Package?

**Yes, strongly recommended.** This should be a **separate NuGet package** — e.g., `Fritz.Aspire.Hosting.Minecraft.Azure`.

**Reasons:**
1. **Not all users need Azure monitoring.** The core package should remain lightweight for users who just want Aspire → Minecraft visualization.
2. **Azure SDK is a heavy dependency.** Adding ~5 MB of Azure SDK packages (and MSAL with its auth complexities) to every Minecraft server container is wasteful if unused.
3. **Separation of concerns.** Azure monitoring is an optional enhancement, not core functionality.
4. **Follows the Azure SDK pattern.** Microsoft's own Aspire components use separate packages for each cloud integration (e.g., `Aspire.Hosting.Azure.Storage`, `Aspire.Hosting.Azure.Sql`).
5. **User opt-in.** Users install the Azure package only when they want Azure resource visualization.

**Proposed package structure:**

```
Fritz.Aspire.Hosting.Minecraft         — Core Aspire + Minecraft (existing)
Fritz.Aspire.Hosting.Minecraft.Azure   — Azure Resource Group monitoring (new)
```

---

## 7. Code Sketch: MinecraftAzureMonitorService

This is a **sketch** showing the shape of a background service that monitors an Azure resource group and feeds data into the existing `ResourceInfo`/`ResourceStatus` model used by the Worker.

```csharp
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.ResourceHealth;
using Azure.ResourceManager.ResourceHealth.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Azure;

/// <summary>
/// Background service that polls an Azure resource group for resources and their health,
/// then translates to the same ResourceInfo/ResourceStatus model the Worker uses.
/// </summary>
internal sealed class MinecraftAzureMonitorService : BackgroundService
{
    private readonly ILogger<MinecraftAzureMonitorService> _logger;
    private readonly string _subscriptionId;
    private readonly string _resourceGroupName;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);

    // Shared state — read by the Worker's visualization services
    private readonly Dictionary<string, AzureResourceSnapshot> _resources = new();

    public IReadOnlyDictionary<string, AzureResourceSnapshot> Resources => _resources;
    public int HealthyCount => _resources.Values.Count(r => r.Status == AzureResourceStatus.Healthy);
    public int TotalCount => _resources.Count;

    public MinecraftAzureMonitorService(
        ILogger<MinecraftAzureMonitorService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _subscriptionId = configuration["Azure:SubscriptionId"]
            ?? throw new InvalidOperationException("Azure:SubscriptionId is required");
        _resourceGroupName = configuration["Azure:ResourceGroupName"]
            ?? throw new InvalidOperationException("Azure:ResourceGroupName is required");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // DefaultAzureCredential works in all environments
        var client = new ArmClient(new DefaultAzureCredential());
        var subscription = client.GetSubscriptionResource(
            new ResourceIdentifier($"/subscriptions/{_subscriptionId}"));

        _logger.LogInformation(
            "Starting Azure monitor for resource group {ResourceGroup} in subscription {Subscription}",
            _resourceGroupName, _subscriptionId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollResourcesAsync(subscription, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error polling Azure resources");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task PollResourcesAsync(
        SubscriptionResource subscription,
        CancellationToken ct)
    {
        // Step 1: List all resources in the resource group
        await foreach (var resource in subscription.GetGenericResourcesAsync(
            filter: $"resourceGroup eq '{_resourceGroupName}'",
            expand: "createdTime,changedTime",
            cancellationToken: ct))
        {
            var name = resource.Data.Name;
            var resourceType = resource.Data.ResourceType.ToString();
            var provisioningState = resource.Data.ProvisioningState ?? "Unknown";

            // Step 2: Try to get Resource Health status
            var healthStatus = await TryGetHealthStatusAsync(resource, ct);

            // Step 3: Map to our status model
            var status = MapToStatus(provisioningState, healthStatus);

            var snapshot = new AzureResourceSnapshot(
                Name: name,
                ResourceType: resourceType,
                Location: resource.Data.Location?.ToString() ?? "unknown",
                ProvisioningState: provisioningState,
                Status: status,
                Tags: resource.Data.Tags?.ToDictionary(t => t.Key, t => t.Value)
                    ?? new Dictionary<string, string>());

            var oldStatus = _resources.TryGetValue(name, out var old) ? old.Status : (AzureResourceStatus?)null;
            _resources[name] = snapshot;

            if (oldStatus != status)
            {
                _logger.LogInformation(
                    "Azure resource {Name} ({Type}): {OldStatus} -> {NewStatus}",
                    name, resourceType, oldStatus?.ToString() ?? "new", status);
            }
        }

        // Step 4: Remove resources that no longer exist
        var currentNames = new HashSet<string>(_resources.Keys);
        // (would compare against enumerated set — omitted for sketch)
    }

    private static async Task<ResourceHealthAvailabilityStateValue?> TryGetHealthStatusAsync(
        GenericResource resource,
        CancellationToken ct)
    {
        // Resource Health is not available for all resource types.
        // Wrap in try-catch and return null if not supported.
        try
        {
            // NOTE: The exact SDK method needs verification.
            // The REST API is:
            //   GET {resourceId}/providers/Microsoft.ResourceHealth/availabilityStatuses/current
            // The SDK should expose this via extension methods on the resource.
            return null; // Placeholder — needs hands-on implementation
        }
        catch
        {
            return null;
        }
    }

    private static AzureResourceStatus MapToStatus(
        string provisioningState,
        ResourceHealthAvailabilityStateValue? healthState)
    {
        // Resource Health takes priority
        if (healthState.HasValue)
        {
            if (healthState.Value == ResourceHealthAvailabilityStateValue.Available)
                return AzureResourceStatus.Healthy;
            if (healthState.Value == ResourceHealthAvailabilityStateValue.Degraded)
                return AzureResourceStatus.Degraded;
            if (healthState.Value == ResourceHealthAvailabilityStateValue.Unavailable)
                return AzureResourceStatus.Unhealthy;
            return AzureResourceStatus.Unknown;
        }

        // Fall back to provisioning state
        return provisioningState switch
        {
            "Succeeded" => AzureResourceStatus.Healthy,
            "Failed" => AzureResourceStatus.Unhealthy,
            "Creating" or "Updating" or "Deleting" => AzureResourceStatus.Unknown,
            _ => AzureResourceStatus.Unknown
        };
    }
}

/// <summary>
/// Snapshot of an Azure resource's state.
/// Mirrors the shape of the Worker's ResourceInfo for consistency.
/// </summary>
internal record AzureResourceSnapshot(
    string Name,
    string ResourceType,
    string Location,
    string ProvisioningState,
    AzureResourceStatus Status,
    Dictionary<string, string> Tags);

/// <summary>
/// Health status enum aligned with our existing model.
/// Adds Degraded to match Azure's four-state model.
/// </summary>
internal enum AzureResourceStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy
}
```

### Integration Points

The `MinecraftAzureMonitorService` would integrate with the existing Worker by:

1. **Registered as a hosted service** in the Worker's DI container (opt-in via env var or extension method)
2. **Exposing `Resources`** dictionary — same pattern as `AspireResourceMonitor`
3. **Feeding the same visualization services** (beacons, holograms, boss bars, etc.) via the existing `ResourceStatusChange` pattern
4. **Azure resource type → Minecraft structure mapping** — e.g., `Microsoft.Compute/virtualMachines` → specific beacon color, `Microsoft.Web/sites` → different color

### Extension Method Shape

```csharp
// In the separate Azure package
public static class MinecraftAzureExtensions
{
    /// <summary>
    /// Connects the Minecraft server to an Azure resource group for visualization.
    /// </summary>
    public static IResourceBuilder<MinecraftServerResource> WithAzureResourceGroup(
        this IResourceBuilder<MinecraftServerResource> builder,
        string subscriptionId,
        string resourceGroupName)
    {
        builder.WithEnvironment("AZURE_SUBSCRIPTION_ID", subscriptionId);
        builder.WithEnvironment("AZURE_RESOURCE_GROUP", resourceGroupName);
        builder.WithEnvironment("ASPIRE_FEATURE_AZURE_MONITOR", "true");
        return builder;
    }
}
```

---

## Open Questions / Needs Verification

1. **Resource Health SDK surface:** The exact C# method to get per-resource availability status via `Azure.ResourceManager.ResourceHealth` v1.0.0 needs hands-on testing. The REST API is well-documented, but the SDK wrapper's extension method pattern is not fully documented in samples.

2. **GenericResource filter by resource group:** The OData filter `resourceGroup eq '{name}'` on `GetGenericResourcesAsync()` needs verification — an alternative is to use the REST API endpoint `GET /subscriptions/{subId}/resourceGroups/{rgName}/resources` which is explicitly scoped. The SDK may expose this via a method on `ResourceGroupResource` that we haven't located.

3. **Resource Health coverage:** Not all Azure resource types support Resource Health. Need to build a fallback matrix — which resource types have health data and which only have provisioning state.

4. **MSAL version conflicts:** If the Aspire host already uses Azure.Identity / MSAL, we need to ensure version alignment. Diamond dependency on `Microsoft.Identity.Client` is a common source of NuGet restore failures.

5. **Existing `ResourceStatus` enum:** Our current enum has only `Unknown`, `Healthy`, `Unhealthy`. Azure has four states (Available, Degraded, Unavailable, Unknown). We may want to add `Degraded` to our enum — this is a minor breaking change in the Worker (internal types, so no public API impact).

---

## References

- [Resource management using the Azure SDK for .NET](https://learn.microsoft.com/en-us/dotnet/azure/sdk/resource-management)
- [Azure.ResourceManager NuGet (v1.13.2)](https://www.nuget.org/packages/Azure.ResourceManager)
- [Azure.Identity NuGet (v1.17.1)](https://www.nuget.org/packages/Azure.Identity)
- [Azure.ResourceManager.ResourceHealth NuGet (v1.0.0)](https://www.nuget.org/packages/Azure.ResourceManager.ResourceHealth)
- [Azure.Monitor.Query.Metrics NuGet (v1.0.0)](https://www.nuget.org/packages/Azure.Monitor.Query.Metrics)
- [DefaultAzureCredential credential chain](https://learn.microsoft.com/en-us/dotnet/azure/sdk/authentication/credential-chains)
- [ARM API throttling (token bucket)](https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/request-limits-and-throttling)
- [Azure Resource Health overview](https://learn.microsoft.com/en-us/azure/service-health/resource-health-overview)
- [Azure Resource Health states: Available, Degraded, Unavailable, Unknown](https://learn.microsoft.com/en-us/azure/service-health/resource-health-overview#health-status)
