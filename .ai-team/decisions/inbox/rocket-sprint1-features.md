# Sprint 1 Feature Decisions — Rocket

**By:** Rocket
**Date:** 2026-02-10
**Issues:** #3, #5, #7, #8, #10

## Decision: Opt-in features via environment variables

**What:** Each Sprint 1 feature (particles, title alerts, weather, boss bar, sounds) is enabled by a dedicated environment variable (`ASPIRE_FEATURE_{NAME}=true`) set via builder extension methods on the hosting side, with conditional service registration in the Worker's `Program.cs`.

**Why:** This follows the existing pattern where `WithMonitoredResource()` communicates with the Worker via env vars. Opt-in ensures backward compatibility — users who don't call `WithBossBar()` get zero additional RCON traffic. The Worker project can't reference the hosting project, so env vars are the clean interface.

**Alternatives considered:**
- Configuration section in appsettings — rejected because Aspire already uses env vars for cross-project config
- Always-on features — rejected because RCON commands have tick budget costs

## Decision: Nullable primary constructor injection for optional services

**What:** The 5 new services are injected into `MinecraftWorldWorker` as nullable parameters with defaults (`ParticleEffectService? particles = null`). The main loop checks `if (feature is not null)` before calling.

**Why:** C# primary constructors support default parameter values, making this pattern concise. The alternative (separate `IHostedService` per feature) would duplicate the health-polling logic or require a pub/sub system.

## Decision: Per-resource vs aggregate feature categorization

**What:** Particles, titles, and sounds fire once per resource status change. Weather and boss bar reflect aggregate fleet health and update once per poll cycle.

**Why:** It wouldn't make sense to set weather per-resource (there's only one weather state). Boss bar similarly shows a single fleet-level percentage. Particles and sounds are per-resource because they're localized effects.

## Decision: State-tracking to avoid redundant RCON commands

**What:** Weather tracks `_lastWeather`, boss bar tracks `_lastValue` and `_lastColor`. Commands are only sent when the value actually changes.

**Why:** RCON commands consume server tick budget. Sending `weather clear` every 10 seconds when weather is already clear wastes ticks. State tracking ensures we only send commands on actual transitions.

## Decision: Health ratio thresholds

**What:**
- Weather: 100% = clear, ≥50% = rain, <50% = thunder
- Boss bar color: 100% = green, ≥50% = yellow, <50% = red

**Why:** The 50% threshold provides a clear midpoint. With 5 monitored resources, degrading from 5/5 to 3/5 triggers rain/yellow, and dropping below 3/5 triggers thunder/red. This gives meaningful visual differentiation.

**Status:** ✅ Implemented. All 5 features compile clean and are integrated into the worker main loop.
