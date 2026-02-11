# Epic: Azure Resource Group → Minecraft Integration

> **Status:** Draft — Discussion Document  
> **Author:** Rhodey (Lead)  
> **Date:** 2026-02-10  
> **Requested by:** Jeffrey T. Fritz

---

## 1. Epic Overview

### What

Connect a live Azure Resource Group to a Minecraft world and visualize its resources — App Services, databases, storage accounts, container apps, and more — using the same village-style structures, beacon towers, boss bars, particles, and guardian mobs that already exist for .NET Aspire resources. Azure resource health maps to in-world state in real time.

### Why would someone do this?

| Audience | Value |
|---|---|
| **Conference speakers** | "Here's my production Azure environment... *in Minecraft*." A demo that nobody forgets. |
| **Educators** | Students learn Azure resource types, dependencies, and health states by walking through a Minecraft village instead of staring at the Azure Portal. |
| **DevOps teams** | A novelty war-room display. Put Minecraft on the big screen during an incident and watch the thunderstorm roll in when your App Service goes down. |
| **Content creators** | Twitch/YouTube content that bridges gaming and cloud engineering audiences. |

The Aspire integration proves the concept works. Azure integration takes it from "cool local demo" to "connected to my real cloud."

### Target Audience

- .NET developers already using Aspire who want to extend the experience to Azure
- DevOps engineers who want a visual (and fun) representation of their infrastructure
- Educators teaching cloud concepts
- Conference/meetup presenters

---

## 2. Architecture

### 2.1 How the Aspire Integration Works Today

```
┌──────────────────┐      ┌──────────────────┐      ┌──────────────────┐
│  Aspire AppHost  │      │  Minecraft       │      │  Paper Server    │
│                  │      │  Worker Service   │      │  (Docker)        │
│  .AddMinecraft() │──────│                  │─RCON─│                  │
│  .WithMonitored  │ env  │  AspireResource   │      │  Holograms       │
│   Resource(api)  │ vars │  Monitor polls   │      │  Structures      │
│  .WithBossBar()  │      │  HTTP/TCP health  │      │  Beacons, etc.   │
└──────────────────┘      └──────────────────┘      └──────────────────┘
```

Key characteristics:
- **Resource discovery:** At startup, the worker reads `ASPIRE_RESOURCE_*` env vars injected by `WithMonitoredResource()`.
- **Health polling:** HTTP GET or TCP socket connect, every 10 seconds.
- **RCON commands:** Worker sends `/fill`, `/setblock`, `/bossbar`, `/title`, etc. to build and update in-world structures.
- **All local:** Everything runs on the developer's machine. No auth, no network latency, no API rate limits.

### 2.2 What Changes for Azure

| Dimension | Aspire (today) | Azure (proposed) |
|---|---|---|
| **Scope** | Local dev machine | Remote cloud subscription |
| **Resource discovery** | Env vars at startup (static) | ARM API — resources can be added/removed at any time |
| **Health data source** | HTTP/TCP probe from worker | Azure Resource Health API + Azure Monitor |
| **Authentication** | None needed | Azure Identity (DefaultAzureCredential, service principal, managed identity) |
| **Metrics** | OpenTelemetry via JVM agent | Azure Monitor Metrics API |
| **Resource count** | Typically 3–8 | Could be 5–200+ in a production RG |
| **Network** | Loopback / Docker bridge | Outbound HTTPS to Azure management plane |
| **Latency** | Sub-millisecond | 200–2000ms per API call |

### 2.3 Proposed Component Architecture

```
┌─────────────────────────────┐
│  Aspire AppHost (optional)  │
│                             │
│  .AddMinecraftServer()      │
│  .WithAzureResourceGroup()  │ ◄── NEW extension method
└───────────┬─────────────────┘
            │ env vars (subscription, RG, auth config)
            ▼
┌─────────────────────────────┐      ┌──────────────────┐
│  Minecraft Worker Service   │      │  Paper Server     │
│                             │      │  (Docker)         │
│  ┌───────────────────────┐  │─RCON─│                  │
│  │ AspireResourceMonitor │  │      │  Structures,     │
│  │ (existing — Aspire)   │  │      │  beacons, etc.   │
│  └───────────────────────┘  │      └──────────────────┘
│  ┌───────────────────────┐  │
│  │ AzureResourceMonitor  │  │ ◄── NEW service
│  │ (ARM + Resource Health)│  │
│  └───────────────────────┘  │
│  ┌───────────────────────┐  │
│  │ AzureResourceMapper   │  │ ◄── Maps Azure types → Minecraft structures
│  └───────────────────────┘  │
└─────────────────────────────┘
            │
            ▼ HTTPS (outbound)
┌─────────────────────────────┐
│  Azure Resource Manager     │
│  Azure Resource Health      │
│  Azure Monitor              │
└─────────────────────────────┘
```

### 2.4 Package Strategy — Three Options

**Option A: Extension of the existing package (Recommended for v1)**

Add Azure support directly into `Fritz.Aspire.Hosting.Minecraft`. The worker gets a new `AzureResourceMonitor` service. The hosting lib gets a new `WithAzureResourceGroup()` extension method.

- ✅ Simplest for consumers — one package
- ✅ Reuses all existing in-world services (beacons, particles, guardian mobs, etc.)
- ❌ Pulls in `Azure.ResourceManager.*` dependencies (~5–10 MB of NuGet packages) even if you don't use Azure
- ❌ Couples Azure SDK version to the main package release cycle

**Option B: Separate NuGet package**

`Fritz.Aspire.Hosting.Minecraft.Azure` — a new package that references the main package and adds Azure-specific types.

- ✅ Clean dependency isolation — Azure SDK only pulled in by consumers who want it
- ✅ Independent versioning
- ✅ Follows .NET ecosystem conventions (e.g., `Aspire.Hosting.Azure.*` packages)
- ❌ Two packages to install
- ❌ Needs a stable abstraction boundary between the main package and the Azure extension

**Option C: Standalone (no Aspire required)**

A completely independent package that connects Minecraft directly to Azure, without requiring .NET Aspire at all.

- ✅ Broadest audience — any .NET dev can use it
- ✅ No Aspire dependency
- ❌ Must duplicate or abstract all of the worker infrastructure
- ❌ Two separate code paths to maintain
- ❌ Loses the "Aspire dashboard + Minecraft" story

**Recommendation:** Start with **Option B** (separate NuGet package). It keeps the dependency graph clean while preserving the Aspire integration story. Option C could follow later as a stretch goal if demand exists.

### 2.5 Polling Model vs Event-Driven

**Polling (Recommended for v1)**
- Simple `Timer` + `ArmClient.GetResources()` every N seconds
- Matches the existing Aspire pattern (`AspireResourceMonitor` polls every 10s)
- Predictable API call volume
- Easy to reason about rate limits

**Event-Driven (Future)**
- Azure Event Grid subscriptions for resource state changes
- Near-real-time updates
- Requires an Event Grid topic, a webhook endpoint, and network ingress to the worker
- Significantly more complex — especially for local dev scenarios
- Could supplement polling for specific high-value events (deployments, scale events)

**Recommendation:** Polling for v1. Event Grid as a "nice-to-have" in a later version.

---

## 3. Azure Resource Type → Minecraft Mapping

> **Note:** The Minecraft structure column contains suggestions. Rocket owns the final in-world designs.

| Azure Resource Type | Minecraft Structure (suggestion) | Rationale |
|---|---|---|
| **App Service** | Watchtower (stone brick, blue banner) | Reuse existing Project pattern — web apps are "projects" |
| **Azure SQL** | Library/Archive (bookshelves, oak frame) | Data storage = books and knowledge |
| **Storage Account** | Warehouse (iron block, chests inside) | Reuse existing Container pattern — storage is "containers" |
| **Key Vault** | Vault (obsidian walls, iron door, enchanting table) | Obsidian = unbreakable, enchanting = secrets/magic |
| **Container Apps** | Warehouse variant (purple glass, shipping containers) | Docker containers → warehouses |
| **AKS (Kubernetes)** | Fortress (nether brick, multiple rooms) | Complex orchestrator → complex structure |
| **Virtual Network** | Walls + Gates (stone brick walls connecting structures) | Network = physical boundaries and pathways |
| **Function App** | Workshop (crafting table, anvil, redstone) | Reuse existing Executable pattern — functions are lightweight |
| **Cosmos DB** | Observatory (glass dome, end stone, purple accents) | "Cosmos" = space/stars theme |
| **Redis Cache** | Redstone Tower (redstone blocks, repeaters) | Redis = fast signals → redstone = fast signals |
| **Service Bus** | Post Office / Rail Station (minecart rails, hoppers) | Message bus = mail delivery system |
| **Event Hub** | Amphitheater (stepped seating, note blocks) | Events broadcast to many listeners → amphitheater |
| **API Management** | Gatehouse (drawbridge, portcullis) | API gateway = castle gate |
| **Application Insights** | Watchtower with spyglass (elevated, glass windows) | Monitoring = looking out |
| **Static Web App** | Billboard / Sign Wall (large sign display) | Static content = posted signs |

### Health State Mapping

Reuse the existing health indicator patterns from the Aspire integration:

| Azure Health State | Minecraft Visual | Existing Pattern? |
|---|---|---|
| **Available / Running** | Green beacon beam, glowstone indicator, iron golem, clear weather | ✅ Yes |
| **Degraded** | Yellow beacon beam, sea lantern, rain weather | ✅ Partially (extend with yellow) |
| **Unavailable / Stopped** | Red beacon beam, redstone lamp, zombie, thunder | ✅ Yes |
| **Unknown / Creating** | Light blue beacon beam, sea lantern, clouds | ✅ Yes |

### Provisioning State Mapping (new — Azure-specific)

| Provisioning State | Minecraft Visual |
|---|---|
| **Creating** | Structure building animation (blocks placed one at a time with particle effects) |
| **Updating** | Scaffolding blocks around structure |
| **Deleting** | Structure crumbles (blocks replaced with air + explosion particles) |
| **Succeeded** | Normal structure (fully built) |
| **Failed** | Ruined structure (cobwebs, cracked stone, fire) |

---

## 4. API Surface Design

### 4.1 Primary API — Aspire Integration

```csharp
// Minimal — just point at a Resource Group
var minecraft = builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithAzureResourceGroup("my-subscription-id", "my-resource-group")
    .WithBossBar()
    .WithBeaconTowers()
    .WithGuardianMobs();
```

```csharp
// Full configuration
var minecraft = builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithAzureResourceGroup(options =>
    {
        options.SubscriptionId = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";
        options.ResourceGroupName = "my-production-rg";
        options.PollingInterval = TimeSpan.FromSeconds(30);
        options.IncludeResourceTypes = ["Microsoft.Web/sites", "Microsoft.Sql/servers"];
        options.ExcludeResourceTypes = ["Microsoft.Network/networkSecurityGroups"];
        options.Credential = new DefaultAzureCredential();
    })
    .WithBossBar("Production Fleet")
    .WithBeaconTowers()
    .WithGuardianMobs();
```

### 4.2 Configuration Options

```csharp
public class AzureResourceGroupOptions
{
    /// <summary>Azure subscription ID.</summary>
    public string SubscriptionId { get; set; } = "";

    /// <summary>Azure resource group name.</summary>
    public string ResourceGroupName { get; set; } = "";

    /// <summary>How often to poll Azure for resource changes. Default: 30 seconds.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Azure credential to use. Defaults to DefaultAzureCredential.
    /// Supports service principal, managed identity, Azure CLI, VS credential, etc.
    /// </summary>
    public TokenCredential? Credential { get; set; }

    /// <summary>
    /// Only include these resource types (ARM type strings).
    /// If empty, all resource types in the RG are included.
    /// Example: ["Microsoft.Web/sites", "Microsoft.Sql/servers"]
    /// </summary>
    public List<string> IncludeResourceTypes { get; set; } = [];

    /// <summary>
    /// Exclude these resource types even if they match IncludeResourceTypes.
    /// Useful for filtering out infrastructure noise (NSGs, route tables, etc.).
    /// </summary>
    public List<string> ExcludeResourceTypes { get; set; } = [];

    /// <summary>
    /// Maximum number of resources to visualize. Default: 50.
    /// Prevents world from becoming unmanageable with large RGs.
    /// </summary>
    public int MaxResources { get; set; } = 50;
}
```

### 4.3 Authentication

| Method | When to Use | Configuration |
|---|---|---|
| `DefaultAzureCredential` (default) | Local dev — uses `az login`, VS credentials, or env vars | No config needed |
| Service Principal | CI/CD, headless environments | Set `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, `AZURE_TENANT_ID` env vars |
| Managed Identity | Running in Azure (ACA, AKS, VM) | Set `AZURE_CLIENT_ID` for user-assigned MI |
| Azure CLI | Quick local testing | Run `az login` before starting |

The `DefaultAzureCredential` chain handles all of these automatically. Consumers can override with a specific `TokenCredential` if they need to.

### 4.4 How It Flows Through the Existing Pattern

The `WithAzureResourceGroup()` extension method would:

1. Set env vars on the worker: `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`, `AZURE_POLLING_INTERVAL`, filter config
2. The worker's `Program.cs` reads those env vars and registers `AzureResourceMonitor` alongside `AspireResourceMonitor`
3. `AzureResourceMonitor` outputs the same `ResourceInfo` and `ResourceStatusChange` records that the existing services consume
4. All existing services (beacons, particles, boss bar, etc.) work without modification — they just see more resources

This is the critical architectural insight: **the Azure monitor is a new resource discovery source, not a new rendering pipeline.** The in-world visualization layer stays the same.

---

## 5. Feature Breakdown

### Phase 1: Core Azure Connection (Size: L)

| # | Feature | Owner | Size | Description |
|---|---|---|---|---|
| 1.1 | Azure ARM client wrapper | Shuri | M | `AzureResourceMonitor` that calls `ArmClient.GetResourceGroup().GetResources()`, maps to `ResourceInfo` |
| 1.2 | Resource discovery polling | Shuri | S | Timer-based polling, diff detection (new resources, removed resources) |
| 1.3 | Resource Health integration | Shuri | M | Call `ResourceHealthResource` API for each discovered resource, map to `ResourceStatus` |
| 1.4 | `WithAzureResourceGroup()` extension | Shuri | S | Hosting extension method + `AzureResourceGroupOptions` |
| 1.5 | Authentication plumbing | Shuri | S | `DefaultAzureCredential` + env var pass-through |
| 1.6 | New NuGet package scaffold | Shuri | S | `Fritz.Aspire.Hosting.Minecraft.Azure` csproj, README, package metadata |

### Phase 2: Azure → Minecraft Mapping (Size: M)

| # | Feature | Owner | Size | Description |
|---|---|---|---|---|
| 2.1 | Azure type → Minecraft structure mapper | Rocket | M | Map ARM resource types to structure styles |
| 2.2 | Provisioning state visuals | Rocket | M | Building animation, scaffolding, crumbling for creating/updating/deleting |
| 2.3 | Dynamic resource add/remove | Rocket | S | Handle structures appearing/disappearing as Azure resources change |
| 2.4 | Azure-specific health indicators | Rocket | S | Extend beacon colors, guardian mobs for Azure health states (Degraded, Unknown) |

### Phase 3: Configuration & Filtering (Size: S)

| # | Feature | Owner | Size | Description |
|---|---|---|---|---|
| 3.1 | Resource type filters | Shuri | S | `IncludeResourceTypes` / `ExcludeResourceTypes` |
| 3.2 | Polling interval config | Shuri | S | Configurable via options, env var fallback |
| 3.3 | Max resource cap | Shuri | S | Prevent world explosion with `MaxResources` |

### Phase 4: Tests (Size: M)

| # | Feature | Owner | Size | Description |
|---|---|---|---|---|
| 4.1 | ARM client unit tests (mocked) | Nebula | M | Test resource discovery, diff detection, health mapping |
| 4.2 | Options validation tests | Nebula | S | Required fields, invalid intervals, filter logic |
| 4.3 | Integration test with Azure (optional) | Nebula | M | Live test against a test RG (CI/CD opt-in, requires secrets) |

### Phase 5: Advanced (Size: L — Future)

| # | Feature | Owner | Size | Description |
|---|---|---|---|---|
| 5.1 | Resource dependency mapping | Shuri + Rocket | L | VNet → subnet → VM relationships visualized as connected structures |
| 5.2 | Cost visualization | Shuri | M | Azure Cost Management API → scoreboard or hologram display |
| 5.3 | Deployment tracking | Shuri | M | ARM deployment operations → in-world building animations |
| 5.4 | Azure Monitor metrics | Shuri | M | CPU%, memory, request count → action bar ticker or holograms |
| 5.5 | Event Grid integration | Shuri | L | Real-time events instead of polling |
| 5.6 | Multi-RG support | Shuri | M | Multiple resource groups as separate "districts" in the world |
| 5.7 | Standalone mode (no Aspire) | Shuri | L | `Fritz.Azure.Minecraft` — direct Azure-to-Minecraft without Aspire |

---

## 6. Open Questions

These need team discussion or Jeff's input before implementation starts.

### Package Naming
- `Fritz.Aspire.Hosting.Minecraft.Azure` vs `Fritz.Azure.Minecraft`?
- If we want standalone (no Aspire) support later, the name matters now. A name with "Aspire" in it signals Aspire-only.
- **Recommendation:** `Fritz.Aspire.Hosting.Minecraft.Azure` for v1 (it is an Aspire integration). Standalone gets its own name later if needed.

### Should This Work Without Aspire?
- The current worker is an Aspire project (`Microsoft.NET.Sdk.Worker` with OpenTelemetry + RCON).
- A standalone mode would need a different host — maybe a console app or a generic host.
- **Recommendation:** Aspire-only for v1. Standalone is Phase 5 — it's a lot of plumbing for uncertain demand.

### Real-Time vs Polling
- Polling is simpler but has a 30-second latency floor.
- Event Grid gives sub-second updates but requires Azure infrastructure setup.
- **Recommendation:** Polling for v1. Polling interval is configurable. Event Grid is Phase 5.

### Scale: What Happens With 50+ Resources?
- The current village layout is a 2-column grid. 50 resources = 25 rows × 10-block spacing = 250 blocks long.
- That's walkable but not fun. Options:
  - Multi-district layout (group by resource type into neighborhoods)
  - Compact view mode (smaller structures, tighter grid)
  - Summary mode (one structure per resource *type* with count, not one per *instance*)
  - `MaxResources` cap with intelligent filtering (skip NSGs, route tables, etc.)
- **Recommendation:** Default `MaxResources = 50`, auto-exclude infrastructure noise (NSGs, route tables, public IPs, etc.), and add resource type grouping in Phase 5.

### Mixed Mode: Aspire Resources + Azure Resources in the Same World?
- If someone uses both `WithMonitoredResource(api)` and `WithAzureResourceGroup(...)`, both monitors discover resources.
- Do they share the same village? Separate districts? What about naming collisions?
- **Recommendation:** Same village, separate index ranges. Aspire resources first, then Azure resources. The existing `VillageLayout.GetStructureOrigin(index)` handles positioning — we just need to offset Azure indices by the Aspire resource count.

### Azure Resource Types to Exclude by Default
- An Azure RG often contains dozens of "infrastructure plumbing" resources that aren't interesting:
  - `Microsoft.Network/networkSecurityGroups`
  - `Microsoft.Network/publicIPAddresses`
  - `Microsoft.Network/routeTables`
  - `Microsoft.ManagedIdentity/userAssignedIdentities`
  - `Microsoft.OperationalInsights/workspaces`
- Should we maintain a default exclude list?
- **Recommendation:** Yes — ship with a sensible default exclude list. Users can override with `IncludeResourceTypes` if they want everything.

---

## 7. Risks & Dependencies

### Azure SDK Dependencies
- `Azure.ResourceManager` alone pulls in `Azure.Core`, `System.Text.Json`, `Azure.Identity`, and several `Azure.ResourceManager.*` packages.
- The current package is ~40 MB (mostly the OTel Java agent). Adding Azure SDK could add 5–10 MB.
- **Mitigation:** Separate NuGet package (Option B) isolates these dependencies. Consumers who don't use Azure never pull them in.

### Authentication Complexity
- `DefaultAzureCredential` is the best default but it has a long chain of fallbacks that can produce confusing errors.
- First-run experience for someone who hasn't done `az login` will be a wall of auth errors.
- **Mitigation:** Clear error messages. A "Getting Started" section in the README. Maybe a health check that validates Azure connectivity before the worker starts building structures.

### Rate Limiting on Azure APIs
- ARM API has throttling limits: ~12,000 reads/hour per subscription (varies by resource provider).
- With 50 resources at 30-second polling: ~6,000 calls/hour just for health checks. Tight but feasible.
- **Mitigation:** Configurable polling interval. Batch API calls where possible. Cache resource lists. Default to 30s, not 10s like the Aspire monitor.

### Minecraft World Scale Limitations
- Paper server `MAX_WORLD_SIZE` is currently set to 256 blocks. Large RGs might exceed this.
- Beacon render distance is 256 blocks — beacons beyond that won't be visible.
- **Mitigation:** `MaxResources` cap. Compact layout mode. Increase `MAX_WORLD_SIZE` if needed.

### Azure Cost
- Polling Azure APIs is free (management plane reads), but Azure Monitor Metrics and Cost Management APIs have their own pricing tiers.
- **Mitigation:** Core features (ARM + Resource Health) use free APIs. Advanced features (metrics, cost) are opt-in.

### Testing
- Unit tests can mock the ARM client. Integration tests need a real Azure subscription.
- **Mitigation:** Mock-based unit tests for CI. Live integration tests are opt-in (require `AZURE_*` secrets in CI).

### RCON Command Volume
- Azure resources may exceed the current 250ms throttle capacity if there are many resources.
- 50 resources × ~15 RCON commands per structure = 750 commands at setup. At 250ms throttle = ~3 minutes for initial world build.
- **Mitigation:** Batch `fill` commands where possible. Consider a "bulk build" mode that temporarily disables throttling. Stagger initial build.

---

## Appendix: Existing Patterns to Reuse

The Azure integration should reuse as many existing patterns as possible:

| Pattern | Where It Exists Today | How Azure Uses It |
|---|---|---|
| `ResourceInfo` / `ResourceStatusChange` records | `AspireResourceMonitor.cs` | `AzureResourceMonitor` emits the same types |
| `StructureBuilder` + `GetStructureType()` | `StructureBuilder.cs` | Extend with new Azure structure types |
| `VillageLayout` grid system | `VillageLayout.cs` | Azure resources placed on the same grid |
| Beacon tower colors by type | `BeaconTowerService.cs` | Add Azure type → color mapping |
| Guardian mobs by health | `GuardianMobService.cs` | Works unchanged — reads `ResourceStatus` |
| Boss bar health percentage | `BossBarService.cs` | Include Azure resources in healthy/total count |
| Opt-in env var pattern | `ASPIRE_FEATURE_*` | Add `AZURE_FEATURE_*` or reuse existing |
| `WithMonitoredResource()` env var injection | `MinecraftServerBuilderExtensions.cs` | `WithAzureResourceGroup()` follows same pattern |

---

*This is a discussion document. Push back on anything that doesn't feel right. The goal is to ship something real, not to design a perfect system.*
