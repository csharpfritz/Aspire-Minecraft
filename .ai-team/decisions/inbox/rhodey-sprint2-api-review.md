# Sprint 2 API Review — Rhodey

**Date:** 2026-02-10
**By:** Rhodey
**Scope:** Public API surface review of `src/Aspire.Hosting.Minecraft/` after Sprint 2 completion

---

## API Consistency Assessment

### ✅ Extension Method Pattern — Consistent

All 10 feature extension methods (5 Sprint 1 + 5 Sprint 2) follow the exact same pattern:

1. **Signature:** `public static IResourceBuilder<MinecraftServerResource> With{Feature}(this IResourceBuilder<MinecraftServerResource> builder)`
2. **Guard clause:** Checks `builder.Resource.WorkerBuilder` with `InvalidOperationException` if null
3. **Env var:** Sets `ASPIRE_FEATURE_{NAME}` = `"true"` on the worker builder
4. **Return:** Returns `builder` for fluent chaining
5. **XML docs:** All methods have `<summary>`, `<param>`, `<returns>`, and `<exception>` tags

| Method | Env Var | Sprint | Notes |
|--------|---------|--------|-------|
| `WithParticleEffects()` | `ASPIRE_FEATURE_PARTICLES` | 1 | ✅ |
| `WithTitleAlerts()` | `ASPIRE_FEATURE_TITLE_ALERTS` | 1 | ✅ |
| `WithWeatherEffects()` | `ASPIRE_FEATURE_WEATHER` | 1 | ✅ |
| `WithBossBar(appName?)` | `ASPIRE_FEATURE_BOSSBAR` | 1 | ✅ Only method with optional param |
| `WithSoundEffects()` | `ASPIRE_FEATURE_SOUNDS` | 1 | ✅ |
| `WithActionBarTicker()` | `ASPIRE_FEATURE_ACTIONBAR` | 2 | ✅ |
| `WithBeaconTowers()` | `ASPIRE_FEATURE_BEACONS` | 2 | ✅ |
| `WithFireworks()` | `ASPIRE_FEATURE_FIREWORKS` | 2 | ✅ |
| `WithGuardianMobs()` | `ASPIRE_FEATURE_GUARDIANS` | 2 | ✅ |
| `WithDeploymentFanfare()` | `ASPIRE_FEATURE_FANFARE` | 2 | ✅ |

### ✅ Environment Variable Naming — Consistent

- All feature toggles: `ASPIRE_FEATURE_{FEATURE_NAME}` = `"true"`
- Resource metadata: `ASPIRE_RESOURCE_{NAME}_TYPE`, `_URL`, `_HOST`, `_PORT`
- App-level: `ASPIRE_APP_NAME`
- No naming collisions. The `ASPIRE_` prefix is a clean namespace.

### ✅ XML Documentation — Complete

Every public method and type has XML docs. All `<param>`, `<returns>`, and `<exception>` tags are present. The RCON types (`TpsResult`, `MsptResult`, `PlayerListResult`, `WorldListResult`) all use `<param>` on their record struct constructor params.

### ✅ Access Modifiers — Correct

- **Public:** `MinecraftServerBuilderExtensions` (16 methods), `MinecraftServerResource`, `RconClient`, `RconConnection`, `RconResponseParser`, `TpsResult`, `MsptResult`, `PlayerListResult`, `WorldListResult`
- **Internal:** `MinecraftHealthCheck`, `RconPacket`, `ModrinthPluginAnnotation`, `AspireWorldDisplayAnnotation`, all Worker service classes
- `EnablePackageValidation` in csproj catches accidental surface changes — good

### ✅ Parameter Ordering — Consistent

The `builder` parameter is always first (as `this`). Optional parameters follow. `WithBossBar(appName?)` is the only method with an additional parameter, and it's correctly optional with default `null`.

---

## Concerns (Non-Breaking — Documentation Only)

### 1. No `WithAllFeatures()` convenience method

With 10 feature methods, the call chain is getting long. Consider adding `WithAllFeatures()` in Sprint 3 as a shortcut. This would not break existing code.

### 2. `WithBossBar()` is the only parameterized feature

The `appName` parameter on `WithBossBar()` is the only feature method that accepts configuration. If Sprint 3 features need parameters (e.g., `WithBeaconTowers(height: 5)`), the precedent is set — but document that parameterized features should keep defaults for zero-config usage.

### 3. Worker `RconService` is sealed with no interface

Flagged by Nebula in Sprint 1. Still applies — `RconService` is `sealed` with no `IRconCommandSender` interface. This makes unit testing worker services harder. Recommend adding an interface in Sprint 3 without breaking the sealed class.

### 4. Feature env var check uses `!string.IsNullOrEmpty` not `== "true"`

In `Worker/Program.cs`, features are enabled by checking `!string.IsNullOrEmpty(builder.Configuration["ASPIRE_FEATURE_X"])`. This means any non-empty value enables the feature (e.g., `"false"` would enable it). Consider tightening to `== "true"` comparison in Sprint 3.

### 5. `ParseConnectionString` duplicated in two places

`MinecraftHealthCheck.ParseConnectionString` and `Worker/Program.cs`'s local `ParseConnectionString` are nearly identical. Consider extracting to a shared utility in the RCON project in Sprint 3.

---

## Recommendations for Sprint 3

1. **Add `WithAllFeatures()` convenience method** — single call to enable all opt-in features
2. **Extract `ParseConnectionString` to shared utility** — eliminate duplication
3. **Add `IRconCommandSender` interface** — unblock worker service unit testing
4. **Tighten feature env var checks** — compare `== "true"` instead of `!string.IsNullOrEmpty`
5. **Consider `WithAllMonitoredResources()` auto-discovery** — if Aspire supports enumerating registered resources, auto-monitor them all
6. **API freeze before v0.2.0** — no public API changes after Sprint 3 review until 1.0

---

## Verdict

**API surface is clean, consistent, and well-documented.** Sprint 2 methods follow Sprint 1 patterns exactly. No breaking changes needed. The 5 concerns above are all additive improvements for Sprint 3.

**Status:** ✅ Approved for v0.1.0 release cut.
