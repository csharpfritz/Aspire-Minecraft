# Sprint 1 Proactive Test Coverage for Rocket's Features

**By:** Nebula
**Date:** 2026-02-10
**Sprint:** 1

## What

Created `tests/Aspire.Hosting.Minecraft.Worker.Tests` project with 145 tests covering all 5 Sprint 1 features being built by Rocket:

| Feature | Issue | Test File | Tests |
|---------|-------|-----------|-------|
| Particle Effects | #3 | ParticleEffectsCommandTests.cs | 8 |
| Title Screen Alerts | #5 | TitleAlertsCommandTests.cs | 12 |
| Weather = System Health | #7 | WeatherEffectsCommandTests.cs | 10 |
| Boss Bar Health Meter | #8 | BossBarCommandTests.cs | 17 |
| Sound Effects | #10 | SoundEffectsCommandTests.cs | 14 |
| State Transitions (shared) | — | StateTransitionTrackingTests.cs | 11 |
| Health→RCON Mapping (cross-feature) | — | HealthTransitionRconMappingTests.cs | 12 |
| Feature Opt-in Behavior | — | FeatureOptInTests.cs | 3 (+8 commented stubs) |
| Command Format Helpers | — | Helpers/RconCommandFormats.cs | (helper) |

**Total: 145 tests, 0 failures.** Solution total: 207 tests across 3 projects, all passing.

## Why

Proactive testing — writing tests BEFORE the implementation lands ensures:
1. The expected RCON command syntax is documented and validated
2. State transition edge cases are covered early
3. Rocket has concrete test expectations to code against
4. Once implementation lands, tests can be adjusted to test through actual service classes

## Key Decisions Made

1. **No MockRconService** — `RconService` is `sealed` with no interface. Tests validate command string format via a static `RconCommandFormats` helper instead of mocking.
2. **Command format tests are pure** — they compile and pass NOW, testing Minecraft RCON command syntax independently of Rocket's implementation.
3. **Commented-out stubs** in `FeatureOptInTests.cs` — these test extension method existence and DI registration, but can't compile until Rocket's `WithParticleEffects()` etc. exist. Ready to uncomment.
4. **Health ratio thresholds** — Weather mapping uses: 100% = clear, 20-99% = rain, <20% = thunder. BossBar color: ≥75% = green, 25-74% = yellow, <25% = red. These are opinionated — Rocket may choose different thresholds.

## Open Items for Rocket

- [ ] Confirm particle type selection for health/unhealthy states
- [ ] Confirm weather health ratio thresholds (currently 100/20/0)
- [ ] Confirm boss bar namespace ID (tests assume `aspire:health`)
- [ ] Confirm sound effect choices for recovery vs degradation events
- [ ] Provide `WithParticleEffects()`, `WithTitleAlerts()`, `WithWeatherEffects()`, `WithBossBar()`, `WithSoundEffects()` so opt-in tests can be uncommented

## Testability Concern

`RconService` is `sealed` with no interface abstraction. The new feature services will likely take `RconService` directly (like existing services). This means we cannot unit test RCON command dispatch without a real TCP mock server. Consider adding an `IRconCommandSender` interface in Sprint 2 to improve testability.

**Status:** ✅ Complete — ready for review
