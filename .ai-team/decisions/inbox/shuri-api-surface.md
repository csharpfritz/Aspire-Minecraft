# Decision: Public API Surface Contract

**By:** Shuri
**Date:** 2026-02-10
**Issue:** #12

## What

Audited all public types across the three source projects and established the intentional public API surface. Changed implementation details from `public` to `internal`.

### Changes Made

**Made internal (Hosting package):**
- `MinecraftHealthCheck` — only instantiated internally by `AddMinecraftServer()`. Consumers never need to create or reference it.

**Made internal (Worker project — all types):**
- `MinecraftMetrics`, `RconService`, `AspireResourceMonitor`, `ResourceInfo`, `ResourceStatusChange`, `ResourceStatus`, `HologramManager`, `PlayerMessageService`, `ScoreboardManager`, `StructureBuilder`, `BossBarService`, `TitleAlertService`, `ParticleEffectService`, `SoundEffectService`, `WeatherService`

**Kept public (Hosting package — intentional public API):**
- `MinecraftServerBuilderExtensions` — consumer entry point (`AddMinecraftServer`, `WithBlueMap`, `WithOpenTelemetry`, `WithDecentHolograms`, `WithAspireWorldDisplay`, `WithMonitoredResource`, `WithParticleEffects`, `WithTitleAlerts`, `WithWeatherEffects`, `WithBossBar`, `WithSoundEffects`)
- `MinecraftServerResource` — resource type consumers reference in generic type parameters

**Kept public (Rcon library — embedded in Hosting package):**
- `RconClient` — low-level RCON protocol client
- `RconConnection` — managed connection with auto-reconnect
- `RconResponseParser` — Minecraft response parsing utilities
- `TpsResult`, `MsptResult`, `PlayerListResult`, `WorldListResult` — response data types

## Why

- The Worker is a standalone service (`IsPackable=false`) — all its types are implementation details with no reason to be public.
- `MinecraftHealthCheck` is an internal concern wired up by `AddMinecraftServer()` — exposing it adds confusion without value.
- The RCON types are kept public because consumers may want to send custom RCON commands or parse responses beyond what the hosting extensions provide.
- `EnablePackageValidation` in `Directory.Build.props` will now catch any accidental API surface changes between package versions.

## Status

✅ Resolved. Build succeeds. All existing tests pass (excluding pre-existing failures in `ParticleEffectsCommandTests` and `WeatherEffectsCommandTests`).
