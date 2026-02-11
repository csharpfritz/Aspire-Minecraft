# API Surface — Fritz.Aspire.Hosting.Minecraft

> **Frozen for:** v0.2.0  
> **Last updated:** 2026-02-10  
> **Package:** `Fritz.Aspire.Hosting.Minecraft`

This document is the authoritative listing of every public type and method shipped in the NuGet package. Any addition, removal, or signature change to a listed item is a breaking/additive API change that requires review.

---

## Namespace: `Aspire.Hosting.Minecraft`

### `MinecraftServerBuilderExtensions` (static class)

All methods return `IResourceBuilder<MinecraftServerResource>` for fluent chaining.

#### Core Server Setup

| Method | Signature | Description |
|--------|-----------|-------------|
| `AddMinecraftServer` | `(this IDistributedApplicationBuilder builder, string name, int? gamePort = null, int? rconPort = null)` | Adds a Minecraft Paper server container with RCON health check. Entry point for all other methods. |
| `WithPersistentWorld` | `(this IResourceBuilder<MinecraftServerResource> builder)` | Persists world data across restarts via a named Docker volume. |
| `WithBlueMap` | `(this IResourceBuilder<MinecraftServerResource> builder, int? port = null)` | Adds BlueMap web map plugin with HTTP endpoint. |
| `WithOpenTelemetry` | `(this IResourceBuilder<MinecraftServerResource> builder)` | Injects OTEL Java agent for JVM metrics/traces/logs. |
| `WithDecentHolograms` | `(this IResourceBuilder<MinecraftServerResource> builder)` | Adds DecentHolograms plugin for in-world hologram displays. |

#### Worker & Resource Monitoring

| Method | Signature | Description |
|--------|-----------|-------------|
| `WithAspireWorldDisplay<TWorkerProject>` | `(this IResourceBuilder<MinecraftServerResource> builder) where TWorkerProject : IProjectMetadata, new()` | Registers the worker service for in-world Aspire display. Must be called before `WithMonitoredResource`. |
| `WithMonitoredResource` | `(this IResourceBuilder<MinecraftServerResource> builder, IResourceBuilder<IResourceWithEndpoints> resource, params string[] dependsOn)` | Adds a resource (with endpoints) to be monitored in-world. |
| `WithMonitoredResource` | `(this IResourceBuilder<MinecraftServerResource> builder, IResourceBuilder<IResource> resource, string resourceType, params string[] dependsOn)` | Adds a resource (without endpoints) to be monitored in-world. |

#### Sprint 1 — Core Feedback Features

| Method | Signature | Env Var | Description |
|--------|-----------|---------|-------------|
| `WithTitleAlerts` | `(this IResourceBuilder<MinecraftServerResource> builder)` | `ASPIRE_FEATURE_TITLE_ALERTS` | Full-screen title alerts on resource state changes. |
| `WithWeatherEffects` | `(this IResourceBuilder<MinecraftServerResource> builder)` | `ASPIRE_FEATURE_WEATHER` | Weather reflects fleet health (clear/rain/thunder). |
| `WithBossBar` | `(this IResourceBuilder<MinecraftServerResource> builder, string? appName = null)` | `ASPIRE_FEATURE_BOSSBAR` | Persistent boss bar showing fleet health %. |
| `WithSoundEffects` | `(this IResourceBuilder<MinecraftServerResource> builder)` | `ASPIRE_FEATURE_SOUNDS` | Audio cues on health state transitions. |
| `WithParticleEffects` | `(this IResourceBuilder<MinecraftServerResource> builder)` | `ASPIRE_FEATURE_PARTICLES` | Smoke/flame on crash, happy villager on recovery. |

#### Sprint 2 — Atmosphere & Delight Features

| Method | Signature | Env Var | Description |
|--------|-----------|---------|-------------|
| `WithActionBarTicker` | `(this IResourceBuilder<MinecraftServerResource> builder)` | `ASPIRE_FEATURE_ACTIONBAR` | Rotating HUD metrics above the hotbar. |
| `WithBeaconTowers` | `(this IResourceBuilder<MinecraftServerResource> builder)` | `ASPIRE_FEATURE_BEACONS` | Per-resource beacon towers with health-colored glass. |
| `WithFireworks` | `(this IResourceBuilder<MinecraftServerResource> builder)` | `ASPIRE_FEATURE_FIREWORKS` | Fireworks on all-green fleet recovery. |
| `WithGuardianMobs` | `(this IResourceBuilder<MinecraftServerResource> builder)` | `ASPIRE_FEATURE_GUARDIANS` | Iron golems (healthy) / zombies (unhealthy) per resource. |
| `WithDeploymentFanfare` | `(this IResourceBuilder<MinecraftServerResource> builder)` | `ASPIRE_FEATURE_FANFARE` | Lightning + fireworks + title on deployment. |

#### Sprint 3 — Showstopper Features

| Method | Signature | Env Var | Description |
|--------|-----------|---------|-------------|
| `WithWorldBorderPulse` | `(this IResourceBuilder<MinecraftServerResource> builder)` | `ASPIRE_FEATURE_WORLDBORDER` | World border shrinks on critical health, expands on recovery. |
| `WithHeartbeat` | `(this IResourceBuilder<MinecraftServerResource> builder)` | `ASPIRE_FEATURE_HEARTBEAT` | Note block pulse reflecting fleet health tempo. |
| `WithAchievements` | `(this IResourceBuilder<MinecraftServerResource> builder)` | `ASPIRE_FEATURE_ACHIEVEMENTS` | Infrastructure milestone achievements. |
| `WithRedstoneDependencyGraph` | `(this IResourceBuilder<MinecraftServerResource> builder)` | `ASPIRE_FEATURE_REDSTONE_GRAPH` | Redstone wire circuits between dependent resources showing DAG. |
| `WithServiceSwitches` | `(this IResourceBuilder<MinecraftServerResource> builder)` | `ASPIRE_FEATURE_SWITCHES` | Visual levers+lamps on structures reflecting service state. |
| `WithPeacefulMode` | `(this IResourceBuilder<MinecraftServerResource> builder)` | `ASPIRE_FEATURE_PEACEFUL` | Eliminates hostile mobs via `/difficulty peaceful` command. |

#### Server Configuration

| Method | Signature | Description |
|--------|-----------|-------------|
| `WithServerProperty` | `(this IResourceBuilder<MinecraftServerResource> builder, string propertyName, string value)` | Sets any `server.properties` value via env var. |
| `WithServerProperty` | `(this IResourceBuilder<MinecraftServerResource> builder, ServerProperty property, string value)` | Sets a property using the `ServerProperty` enum. |
| `WithServerProperties` | `(this IResourceBuilder<MinecraftServerResource> builder, Dictionary<string, string> properties)` | Sets multiple properties at once. |
| `WithServerPropertiesFile` | `(this IResourceBuilder<MinecraftServerResource> builder, string filePath)` | Loads properties from a `server.properties` file. |
| `WithGameMode` | `(this IResourceBuilder<MinecraftServerResource> builder, string mode)` | Sets game mode (string). |
| `WithGameMode` | `(this IResourceBuilder<MinecraftServerResource> builder, MinecraftGameMode mode)` | Sets game mode (enum). |
| `WithDifficulty` | `(this IResourceBuilder<MinecraftServerResource> builder, string difficulty)` | Sets difficulty (string). |
| `WithDifficulty` | `(this IResourceBuilder<MinecraftServerResource> builder, MinecraftDifficulty difficulty)` | Sets difficulty (enum). |
| `WithMaxPlayers` | `(this IResourceBuilder<MinecraftServerResource> builder, int maxPlayers)` | Sets max player count. |
| `WithMotd` | `(this IResourceBuilder<MinecraftServerResource> builder, string motd)` | Sets message of the day. |
| `WithWorldSeed` | `(this IResourceBuilder<MinecraftServerResource> builder, string seed)` | Sets world generation seed. |
| `WithPvp` | `(this IResourceBuilder<MinecraftServerResource> builder, bool enabled = true)` | Enables/disables PvP. |

---

### `MinecraftServerResource` (class)

```csharp
public class MinecraftServerResource : ContainerResource, IResourceWithConnectionString
```

| Member | Type | Description |
|--------|------|-------------|
| `.ctor(string name)` | Constructor | Creates a new Minecraft server resource. |
| `RconPasswordParameter` | `ParameterResource?` | The RCON password parameter. |
| `ConnectionStringExpression` | `ReferenceExpression` | Connection string: `Host=…;Port=…;Password=…` |

---

### `ServerProperty` (enum — 24 members)

`MaxPlayers`, `Motd`, `Difficulty`, `GameMode`, `Pvp`, `Hardcore`, `ViewDistance`, `SimulationDistance`, `MaxWorldSize`, `SpawnProtection`, `SpawnAnimals`, `SpawnMonsters`, `SpawnNpcs`, `AllowFlight`, `AllowNether`, `ForceGamemode`, `LevelType`, `LevelName`, `Seed`, `WhiteList`, `OnlineMode`, `EnableCommandBlock`, `ServerPort`, `MaxBuildHeight`, `GenerateStructures`

### `MinecraftGameMode` (enum — 4 members)

`Survival`, `Creative`, `Adventure`, `Spectator`

### `MinecraftDifficulty` (enum — 4 members)

`Peaceful`, `Easy`, `Normal`, `Hard`

---

## Namespace: `Aspire.Hosting.Minecraft.Rcon`

These types are public for consumers who want to send custom RCON commands.

### `RconClient` (sealed class)

```csharp
public sealed class RconClient : IAsyncDisposable
```

### `RconConnection` (sealed class)

```csharp
public sealed class RconConnection : IAsyncDisposable
```

### `RconResponseParser` (static partial class)

Parses RCON responses into structured results.

### Result Types

| Type | Properties |
|------|------------|
| `TpsResult` | `OneMinute`, `FiveMinute`, `FifteenMinute` (all `double`) |
| `MsptResult` | `FiveSecond`, `TenSecond`, `SixtySecond` (all `double`) |
| `PlayerListResult` | `Online` (`int`), `Max` (`int`), `Players` (`string[]`) |
| `WorldListResult` | `Worlds` (`string[]`) |

---

## API Design Conventions

1. **Fluent builder pattern** — all `With*()` methods return the same `IResourceBuilder<MinecraftServerResource>` for chaining.
2. **Opt-in features** — each feature is enabled via `ASPIRE_FEATURE_{NAME}=true` env var on the worker.
3. **Guard clauses** — feature methods that require the worker throw `InvalidOperationException` if `WithAspireWorldDisplay()` wasn't called first.
4. **XML documentation** — every public type and method has `<summary>`, `<param>`, and `<returns>` tags.
5. **No internal type leakage** — `WorkerBuilder`, `MonitoredResourceNames`, annotations, and all Worker types are `internal`.
