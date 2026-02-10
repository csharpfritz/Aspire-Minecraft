### 2026-02-10: E2E cascade failure scenario + 25-resource performance tests

**By:** Nebula
**Issue:** #31

**What:** Added comprehensive test coverage for Sprint 2 features and beyond:

1. **Sprint 2 feature integration tests** — GuardianMobService (8 tests), BeaconTowerService (16 tests including `GetGlassBlock` unit tests), FireworksService (7 tests), DeploymentFanfareService (7 tests), ActionBarTickerService (5 tests). All follow the established MockRconServer integration test pattern.

2. **E2E cascade failure scenario** (`Scenarios/CascadeFailureScenarioTests.cs`) — 4 tests exercising multi-service interaction: 5 resources healthy → 1 fails → 2 more cascade → boss bar drops to red → guardians switch to zombies → all recover → fireworks launch → golems return. Validates the full event-driven lifecycle across BossBar, Guardians, Fireworks, Fanfare, Beacons, and Particles simultaneously.

3. **25-resource performance tests** (`Performance/LargeResourceSetTests.cs`) — 10 tests proving StructureBuilder, BeaconTowerService, HologramManager, BossBarService, GuardianMobService, and ParticleEffectService all handle 25 resources without exceptions. Verifies VillageLayout 2×13 grid correctness, all-positions-unique, and 100+ RCON commands in a full cycle with no drops.

**Key decisions:**
- Extended `TestResourceMonitorFactory` with `SetResourcesWithTypes()` to support resource-type-specific testing (beacon colors, structure types).
- Used individual `[Fact]` tests instead of `[Theory]` with `[InlineData]` for `GetGlassBlock` because `ResourceStatus` is internal and can't appear in public method signatures.
- Cascade scenario test validates command *ordering* (kill before summon) and service *interaction* (fireworks only after all resources recover), not just individual service behavior.

**Verified:** 303 tests across 3 projects, 0 failures.
**Status:** ✅ Complete.
