# MonitorAllResources Convenience API — Design Document

**Author:** Rhodey (Lead)
**Date:** 2026-02-12
**Status:** Draft
**Related:** [Famous Buildings Design](famous-buildings-design.md), [Sprint 4 Design](sprint-4-design.md)

---

## 1. Motivation

The current AppHost requires a manual `.WithMonitoredResource()` call for every Aspire resource the user wants visualized in Minecraft. In a typical demo with 5–8 resources, this produces 5–8 nearly identical boilerplate lines:

```csharp
.WithMonitoredResource(api)
.WithMonitoredResource(web)
.WithMonitoredResource(redis)
.WithMonitoredResource(pg)
.WithMonitoredResource(anotherRedis);
```

Jeff's directive: replace all of these with a single `.MonitorAllResources()` call.

---

## 2. API Surface

### 2.1 Method Signature

```csharp
public static IResourceBuilder<MinecraftServerResource> MonitorAllResources(
    this IResourceBuilder<MinecraftServerResource> builder)
```

- Extension method on `IResourceBuilder<MinecraftServerResource>`, same as all other `With*` methods.
- Returns `IResourceBuilder<MinecraftServerResource>` for fluent chaining.
- **Guard clause:** Throws `InvalidOperationException` if `WorkerBuilder` is null (same gate as `WithMonitoredResource` — requires `WithAspireWorldDisplay()` first).

### 2.2 Behavior

For every resource in `builder.ApplicationBuilder.Resources`, the method:

1. Checks if the resource should be **excluded** (see §3).
2. Checks if the resource is **already monitored** (already in `MonitoredResourceNames`) — skips to avoid duplicates.
3. Determines if the resource implements `IResourceWithEndpoints` — if yes, delegates to the existing `WithMonitoredResource(builder, resourceBuilder, dependsOn)` overload for endpoint-bearing resources. If no, delegates to the `WithMonitoredResource(builder, resourceBuilder, resourceType, dependsOn)` overload for endpoint-less resources.
4. Auto-detects `IResourceWithParent` to populate `dependsOn` (already handled by `WithMonitoredResource` internally).
5. Preserves any `FamousBuildingAnnotation` already present on the resource (the annotation travels with the resource; `WithMonitoredResource` reads it during env var callback resolution).

### 2.3 Sample AppHost — After

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");
var anotherRedis = builder.AddRedis("another-cache");
var pg = builder.AddPostgres("db-host");
var db = pg.AddDatabase("db");

var api = builder.AddProject<Projects.GrandVillageDemo_ApiService>("api")
    .WithReference(redis);

var web = builder.AddProject<Projects.GrandVillageDemo_Web>("web")
    .WithReference(api)
    .WithExternalHttpEndpoints();

var minecraft = builder.AddMinecraftServer("minecraft", gamePort: 25565, rconPort: 25575)
    .WithBlueMap(port: 8100)
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    // Sprint 1–4 features...
    .WithTitleAlerts()
    .WithBossBar("Minecraft Demo")
    // ...etc...
    .MonitorAllResources();  // ← replaces all WithMonitoredResource calls
```

**Line count reduction:** 5 `.WithMonitoredResource()` calls → 1 `.MonitorAllResources()` call.

---

## 3. Exclusion Strategy

### 3.1 Excluded Resources

`MonitorAllResources` MUST exclude the following from auto-discovery:

| Resource | How to Identify | Rationale |
|---|---|---|
| The Minecraft server itself | `resource == builder.Resource` (same object reference) | Can't monitor yourself |
| The worker project | `resource.Name == builder.Resource.WorkerBuilder.Resource.Name` | Internal infrastructure, not a user resource |
| BlueMap sidecar (if any) | Resource with `MinecraftServerResource.BlueMapEndpointName` endpoint, or resource name matching `{minecraftName}-bluemap-*` pattern | Part of Minecraft hosting, not user's app |
| Any resource that is a child of the Minecraft server | `resource is IResourceWithParent { Parent: MinecraftServerResource }` | Internal Minecraft infrastructure |

### 3.2 Implementation — Exclusion Check

```csharp
private static bool IsMinecraftInfrastructure(
    IResource resource,
    MinecraftServerResource minecraftServer)
{
    // The Minecraft server itself
    if (ReferenceEquals(resource, minecraftServer))
        return true;

    // The worker project
    if (minecraftServer.WorkerBuilder is not null &&
        ReferenceEquals(resource, minecraftServer.WorkerBuilder.Resource))
        return true;

    // Any child of the Minecraft server (BlueMap sidecar, future sidecars)
    if (resource is IResourceWithParent parentResource &&
        ReferenceEquals(parentResource.Parent, minecraftServer))
        return true;

    return false;
}
```

This approach is **structural, not name-based** — it uses object identity and Aspire's parent/child graph. It automatically covers BlueMap and any future sidecars without maintaining a name-matching allowlist.

### 3.3 ExcludeFromMonitoring — Opt-Out API

**Recommendation: Yes, add it.**

```csharp
public static IResourceBuilder<T> ExcludeFromMonitoring<T>(
    this IResourceBuilder<T> builder)
    where T : IResource
{
    builder.WithAnnotation(new ExcludeFromMonitoringAnnotation());
    return builder;
}

internal class ExcludeFromMonitoringAnnotation : IResourceAnnotation;
```

Usage:

```csharp
var internal = builder.AddProject<Projects.InternalService>("internal-svc")
    .ExcludeFromMonitoring();  // won't appear in Minecraft world

// ...later...
minecraft.MonitorAllResources();  // skips internal-svc
```

The exclusion check in `IsMinecraftInfrastructure` becomes:

```csharp
// User opted out
if (resource.Annotations.OfType<ExcludeFromMonitoringAnnotation>().Any())
    return true;
```

### 3.4 Duplicate Prevention

If the user calls `.WithMonitoredResource(api)` **before** `.MonitorAllResources()`, the resource name is already in `MonitoredResourceNames`. `MonitorAllResources` checks `MonitoredResourceNames.Contains(resource.Name)` and skips it.

If the user calls `.WithMonitoredResource(api)` **after** `.MonitorAllResources()`, it works additively — the existing `WithMonitoredResource` adds to `MonitoredResourceNames` and sets additional env vars. The worker handles duplicate env var keys by last-write-wins, but since the values are identical, this is safe.

---

## 4. Interaction with WithMonitoredResource and AsMinecraftFamousBuilding

### 4.1 Famous Building Annotations

`AsMinecraftFamousBuilding()` stores a `FamousBuildingAnnotation` on the resource itself (not on the Minecraft server). When `MonitorAllResources` iterates resources and calls `WithMonitoredResource` internally, the existing `WithMonitoredResource` logic already reads annotations from the monitored resource during the deferred env var callback. **No changes needed** — the annotation flow is inherently call-order-independent.

```csharp
// This works regardless of call order:
var api = builder.AddProject<Projects.Api>("api")
    .AsMinecraftFamousBuilding(FamousBuilding.Pyramid);

minecraft.MonitorAllResources();  // discovers api, sees FamousBuildingAnnotation, passes it through
```

### 4.2 Manual WithMonitoredResource Calls

| Scenario | Behavior |
|---|---|
| `WithMonitoredResource(api)` THEN `MonitorAllResources()` | `api` already in `MonitoredResourceNames` → skipped. No duplicate. |
| `MonitorAllResources()` THEN `WithMonitoredResource(api)` | `api` already in `MonitoredResourceNames` → `WithMonitoredResource` adds duplicate env vars with identical values. Safe. |
| `MonitorAllResources()` THEN `WithMonitoredResource(api, "custom-dep")` | Additive. User wants explicit dependency not auto-detected. The second call's `DEPENDS_ON` env var overwrites the auto-detected one. This is intentional — manual override wins. |

---

## 5. Resource Discovery Timing

### 5.1 Options

**Option A: Eager (at call time)**
- Iterate `builder.ApplicationBuilder.Resources` immediately when `MonitorAllResources()` is called.
- Simple. Deterministic. Easy to debug.
- **Risk:** Misses resources added after the call. Forces `MonitorAllResources()` to be the **last** method called.

**Option B: Deferred (via eventing)**
- Subscribe to `BeforeStartEvent` to discover resources at build time.
- Captures all resources regardless of call order.
- More complex. Harder to debug (lazy execution). Must handle the env var callback context correctly.

### 5.2 Recommendation: Option A — Eager Discovery

**Rationale:**

1. **Aspire AppHost convention is top-down.** Resources are defined in order, then composed. `MonitorAllResources()` is naturally called at the end of the chain, after all resources exist. The sample code already shows this pattern.

2. **Predictability trumps flexibility.** Deferred discovery means the user can't inspect `MonitoredResourceNames` after the call to verify what was picked up. Eager discovery gives immediate feedback.

3. **Consistency with WithMonitoredResource.** The existing method is eager. `MonitorAllResources` should behave the same way — it's a convenience wrapper, not a new paradigm.

4. **Call-order constraint is trivially satisfied.** The natural coding pattern is: define resources → configure Minecraft → chain MonitorAllResources last. The constraint is self-documenting.

5. **Deferred approach has architectural risk.** The `BeforeStartEvent` fires after `Build()` but before `Run()`. At that point, the resource builder pipeline has already been finalized. Calling `WithMonitoredResource` (which calls `workerBuilder.WithEnvironment`) during `BeforeStartEvent` may encounter builder-state issues. This would require investigation and testing against Aspire internals — unnecessary complexity.

**Mitigation for late-added resources:** If a user adds resources after `MonitorAllResources()`, they can still call `.WithMonitoredResource()` manually for those specific resources. The two approaches compose cleanly.

### 5.3 Documentation Requirement

The XML doc comment MUST state:

> Discovers all resources currently registered in the application builder. Resources added after this call are not included — use `WithMonitoredResource()` for those.

---

## 6. Implementation Sketch

```csharp
/// <summary>
/// Automatically monitors all non-Minecraft resources in the Aspire application.
/// Discovers all resources currently registered in the application builder.
/// Resources added after this call are not included — use
/// <see cref="WithMonitoredResource(IResourceBuilder{MinecraftServerResource}, IResourceBuilder{IResourceWithEndpoints}, string[])"/>
/// for those.
/// Requires <see cref="WithAspireWorldDisplay{TWorkerProject}"/> to be called first.
/// </summary>
/// <param name="builder">The Minecraft server resource builder.</param>
/// <returns>The resource builder for chaining.</returns>
/// <exception cref="InvalidOperationException">
/// Thrown when WithAspireWorldDisplay() has not been called first.
/// </exception>
public static IResourceBuilder<MinecraftServerResource> MonitorAllResources(
    this IResourceBuilder<MinecraftServerResource> builder)
{
    _ = builder.Resource.WorkerBuilder
        ?? throw new InvalidOperationException(
            "MonitorAllResources() requires WithAspireWorldDisplay() to be called first.");

    foreach (var resource in builder.ApplicationBuilder.Resources)
    {
        // Skip Minecraft infrastructure
        if (IsMinecraftInfrastructure(resource, builder.Resource))
            continue;

        // Skip user-excluded resources
        if (resource.Annotations.OfType<ExcludeFromMonitoringAnnotation>().Any())
            continue;

        // Skip already-monitored resources
        if (builder.Resource.MonitoredResourceNames.Contains(resource.Name))
            continue;

        // Route to appropriate overload based on endpoint support
        if (resource is IResourceWithEndpoints endpointResource)
        {
            // Wrap in a ResourceBuilder adapter to call existing WithMonitoredResource
            var resourceBuilder = builder.ApplicationBuilder
                .CreateResourceBuilder(endpointResource);
            builder = builder.WithMonitoredResource(resourceBuilder);
        }
        else
        {
            // Endpoint-less resource — pass type name
            var resourceType = resource.GetType().Name.Replace("Resource", "");
            var resourceBuilder = builder.ApplicationBuilder
                .CreateResourceBuilder(resource);
            builder = builder.WithMonitoredResource(resourceBuilder, resourceType);
        }
    }

    return builder;
}
```

> **Note:** `builder.ApplicationBuilder.CreateResourceBuilder()` is a placeholder. The actual Aspire API to wrap an `IResource` back into an `IResourceBuilder<T>` needs verification. If `IDistributedApplicationBuilder` doesn't expose this, the implementation should extract the env var logic from `WithMonitoredResource` into a shared private method that accepts `IResource` directly, avoiding the need to reconstruct builders.

### 6.1 Alternative: Direct Resource Processing

If `CreateResourceBuilder` is not available, refactor `WithMonitoredResource` internals into a shared method:

```csharp
private static IResourceBuilder<MinecraftServerResource> MonitorResource(
    IResourceBuilder<MinecraftServerResource> builder,
    IResource resource)
{
    var workerBuilder = builder.Resource.WorkerBuilder!;
    var name = resource.Name;
    builder.Resource.MonitoredResourceNames.Add(name);

    var resourceType = resource.GetType().Name.Replace("Resource", "");
    workerBuilder.WithEnvironment($"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_TYPE", resourceType);

    // Auto-detect parent dependencies
    if (resource is IResourceWithParent parentResource)
    {
        var parentName = parentResource.Parent.Name;
        workerBuilder.WithEnvironment(
            $"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_DEPENDS_ON", parentName);
    }

    // Resolve endpoints if available
    if (resource is IResourceWithEndpoints resourceWithEndpoints)
    {
        workerBuilder.WithEnvironment(context =>
        {
            EndpointReference? httpRef = null;
            EndpointReference? firstRef = null;
            foreach (var ep in resourceWithEndpoints.GetEndpoints())
            {
                firstRef ??= ep;
                if (string.Equals(ep.EndpointName, "http", StringComparison.OrdinalIgnoreCase))
                { httpRef = ep; break; }
                if (string.Equals(ep.EndpointName, "https", StringComparison.OrdinalIgnoreCase))
                { httpRef = ep; }
            }

            if (httpRef is not null)
                context.EnvironmentVariables[$"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_URL"] =
                    httpRef.Property(EndpointProperty.Url);
            else if (firstRef is not null)
            {
                context.EnvironmentVariables[$"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_HOST"] =
                    firstRef.Property(EndpointProperty.Host);
                context.EnvironmentVariables[$"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_PORT"] =
                    firstRef.Property(EndpointProperty.Port);
            }
        });
    }

    return builder;
}
```

This avoids the `CreateResourceBuilder` dependency entirely. `WithMonitoredResource` can also be refactored to delegate to this method, reducing code duplication.

---

## 7. New Types

| Type | Visibility | Purpose |
|---|---|---|
| `ExcludeFromMonitoringAnnotation` | `internal` | Annotation marking a resource as excluded from `MonitorAllResources` |

No new public types beyond the extension method itself.

---

## 8. Testing Strategy

| Test | What It Verifies |
|---|---|
| `MonitorAllResources_discovers_all_user_resources` | Redis, Postgres, Project resources all appear in `MonitoredResourceNames` |
| `MonitorAllResources_excludes_minecraft_server` | The Minecraft server itself is not in `MonitoredResourceNames` |
| `MonitorAllResources_excludes_worker` | The worker project is not monitored |
| `MonitorAllResources_excludes_minecraft_children` | Resources with `IResourceWithParent` pointing to the Minecraft server are excluded |
| `MonitorAllResources_excludes_annotated_resources` | Resources with `ExcludeFromMonitoringAnnotation` are skipped |
| `MonitorAllResources_skips_already_monitored` | Calling `WithMonitoredResource(api)` before `MonitorAllResources()` doesn't duplicate `api` |
| `MonitorAllResources_throws_without_world_display` | `InvalidOperationException` when `WorkerBuilder` is null |
| `MonitorAllResources_detects_parent_dependencies` | `IResourceWithParent` auto-populates `DEPENDS_ON` env var |
| `MonitorAllResources_respects_famous_building` | Resource with `FamousBuildingAnnotation` gets the annotation passed through |

---

## 9. Migration Guide

### Before (v0.3.x)
```csharp
var minecraft = builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Worker>()
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(pg)
    .WithMonitoredResource(anotherRedis);
```

### After (v0.4.0+)
```csharp
var minecraft = builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Worker>()
    .MonitorAllResources();
```

### Mixed (opt-out specific resources)
```csharp
var internal = builder.AddProject<Projects.Internal>("internal-svc")
    .ExcludeFromMonitoring();

var minecraft = builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Worker>()
    .MonitorAllResources();
// internal-svc is excluded; everything else is monitored
```

### Mixed (manual override for custom dependencies)
```csharp
var minecraft = builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Worker>()
    .WithMonitoredResource(api, "custom-dep-1", "custom-dep-2")  // manual with custom deps
    .MonitorAllResources();  // auto-discovers the rest, skips api (already monitored)
```

---

## 10. Non-Goals

- **Dynamic resource discovery at runtime.** This is a build-time API only. Hot-reload of resources is out of scope.
- **Filtering by resource type.** "Monitor only databases" or "monitor only projects" are not supported. Use `ExcludeFromMonitoring()` for opt-out.
- **Replacing WithMonitoredResource.** The manual API remains for users who want fine-grained control or custom dependency declarations.

---

## 11. Open Questions

1. **Naming: `MonitorAllResources()` vs `WithAllMonitoredResources()`?** The current convention uses `With*` for features. However, `MonitorAllResources` reads more naturally as a verb phrase and distinguishes itself from the per-resource `WithMonitoredResource`. **Recommendation:** Keep `MonitorAllResources()` — it's a distinct convenience method, not a `With*` feature toggle.

2. **Should `ExcludeFromMonitoring` be in v1 or deferred?** It's small (annotation + 1-line check) and completes the story. **Recommendation:** Ship together.
