# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** Aspire.Hosting.Minecraft — .NET Aspire integration for Minecraft servers
- **Stack:** C#, .NET 10, Docker, Aspire, OpenTelemetry, Minecraft Paper Server, RCON
- **Created:** 2026-02-10

## Key Facts

- Three NuGet packages, all at version 0.1.0
- No test projects exist yet — test infrastructure needs to be created
- RCON protocol has complex edge cases (reconnection, response parsing, timeouts)
- Worker service polls metrics and manages in-world state
- Health checks use MinecraftHealthCheck.cs

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

📌 E2E cascade failure scenario test (2026-02-10): Created `tests/Aspire.Hosting.Minecraft.Worker.Tests/Scenarios/CascadeFailureScenarioTests.cs` with 4 tests covering the full cascade failure → recovery lifecycle across 6 services (BossBar, Guardians, Fireworks, Fanfare, Beacons, Particles). Validates command sequences, boss bar percentage thresholds, guardian mob type transitions, beacon glass color tracking, and firework launch on all-green recovery.
📌 25-resource performance tests (2026-02-10): Created `tests/Aspire.Hosting.Minecraft.Worker.Tests/Performance/LargeResourceSetTests.cs` with 10 tests. All services (StructureBuilder, BeaconTower, HologramManager, BossBar, GuardianMobs, ParticleEffects, Fireworks) handle 25 resources without exceptions. VillageLayout correctly positions all 25 in a 2×13 grid. Full update cycle sends 100+ RCON commands with no drops.
📌 Sprint 2 feature integration tests (2026-02-10): Added integration tests for all 5 Sprint 2 features — GuardianMobService (8 tests), BeaconTowerService (16 tests), FireworksService (7 tests), DeploymentFanfareService (7 tests), ActionBarTickerService (5 tests). All use MockRconServer pattern. Total: 303 tests across 3 projects, all passing.
📌 TestResourceMonitorFactory.SetResourcesWithTypes helper added for testing resource-type-specific behavior (beacon colors, structure types). Takes (name, type, ResourceStatus) tuples instead of (name, bool).
📌 ResourceStatus is internal — cannot use in public [Theory] [InlineData] parameters. Use individual [Fact] tests instead when testing internal enum values.

📌 Team update (2026-02-10): NuGet readiness audit completed — pack output needs smoke testing — decided by Shuri
📌 Team update (2026-02-10): 18 features proposed — Nebula will test all Rocket features per sprint — decided by Rocket
📌 Team update (2026-02-10): 3-sprint roadmap adopted — Sprint 1 assigns Nebula: test project structure, RCON unit tests, health check tests, pack smoke test — decided by Rhodey

📌 Sprint 1 test infrastructure (2026-02-10): Created two xUnit test projects under tests/ — Aspire.Hosting.Minecraft.Rcon.Tests and Aspire.Hosting.Minecraft.Tests. 62 tests total, all passing.
📌 InternalsVisibleTo added to both source projects for test access to internal types (RconPacket, endpoint name constants, WorkerBuilder, ParseConnectionString).
📌 Changed MinecraftHealthCheck.ParseConnectionString from private to internal to enable direct unit testing of connection string parsing logic.
📌 RconResponseParser is fully testable — all methods are public static, pure functions. Best coverage target in the codebase.
📌 RconClient requires a mock TCP server for protocol-level testing. Created a pattern using TcpListener on loopback port 0 with manual RCON packet read/write.
📌 RconConnection is hard to unit test in isolation — it creates RconClient internally with no DI seam. Integration tests with a real server needed for reconnection/backoff logic.
📌 FluentAssertions v8 (resolved as 8.8.0) has commercial licensing (Xceed). Team should evaluate switching to a free alternative for open-source compatibility.
📌 MinecraftServerBuilderExtensions.AddMinecraftServer needs full Aspire DI (DistributedApplication.CreateBuilder) for integration-level testing — deferred to Sprint 2.

📌 Team update (2026-02-10): NuGet hardening completed — source projects now have pinned deps and SourceLink — decided by Shuri
📌 Team update (2026-02-10): CI/CD pipeline created — tests will run in ubuntu+windows matrix via build.yml — decided by Wong

📌 FluentAssertions removed — replaced with xUnit built-in Assert (2026-02-10). Zero new dependencies added. Chose xUnit Assert over Shouldly/TUnit because all 62 tests used straightforward assertion patterns (equality, boolean, null, empty, contains, throws) that map 1:1 to Assert.*. No licensing concerns.
📌 Migration patterns: `.Should().Be(x)` → `Assert.Equal(x, actual)`, `.Should().BeTrue/BeFalse()` → `Assert.True/False()`, `.Should().BeNull()` → `Assert.Null()`, `.Should().BeEmpty()` → `Assert.Empty()`, `.Should().Contain(x)` → `Assert.Contains(x, actual)`, `.Should().BeEquivalentTo([...])` → `Assert.Equivalent(expected, actual)`, `.Should().HaveCount(n)` → `Assert.Equal(n, actual.Length)`, `.Should().ThrowAsync<T>()` → `await Assert.ThrowsAsync<T>(...)`, `.Should().BePositive()` → `Assert.True(x > 0)`, `.Should().BeGreaterThan(x)` → `Assert.True(a > x)`.

📌 Sprint 1 proactive tests (2026-02-10): Created Aspire.Hosting.Minecraft.Worker.Tests project with 145 tests for Sprint 1 features (Particles #3, Titles #5, Weather #7, BossBar #8, Sounds #10). All 145 pass. Tests validate RCON command format, state transition logic, health-to-command mapping, coordinate integration with StructureBuilder, and edge cases. Total: 207 tests across 3 projects.
📌 RconService is sealed with no DI seam — cannot mock via inheritance. Sprint 1 feature tests use RconCommandFormats helper to validate command string correctness without requiring a TCP connection. When Rocket's services land, tests may need adjustment to test through the actual service classes.
📌 Worker project (Sdk.Worker) can be referenced by test projects despite being an executable. No InternalsVisibleTo needed since all service types (RconService, AspireResourceMonitor, ResourceInfo, ResourceStatusChange, ResourceStatus) are public.
📌 StructureBuilder uses BaseX=10, BaseY=-60, BaseZ=0, Spacing=6. Particles should target center-above: (BaseX + index*Spacing + 1, BaseY + 2, BaseZ + 1). Tests validate coordinate consistency across features.
📌 Proactive test pattern: when implementation doesn't exist yet, test the command format layer and state logic independently. Commented-out test stubs (FeatureOptInTests) are ready to uncomment once Rocket's WithParticleEffects()/WithTitleAlerts()/WithWeatherEffects()/WithBossBar()/WithSoundEffects() extension methods land.

📌 Team update (2026-02-10): Single NuGet package consolidation — Rcon embedded into Aspire.Hosting.Minecraft, Worker stays separate but IsPackable=false. All 62 tests still pass. — decided by Jeffrey T. Fritz, Shuri
📌 Team update (2026-02-10): Redstone Dependency Graph + Service Switches proposed as Sprint 3 flagship feature — decided by Jeffrey T. Fritz

📌 Team update (2026-02-10): NuGet PackageId renamed from Aspire.Hosting.Minecraft to Fritz.Aspire.Hosting.Minecraft (Aspire.Hosting prefix reserved by Microsoft) — decided by Jeffrey T. Fritz, Shuri

📌 Team update (2026-02-10): NuGet package version now defaults to 0.1.0-dev; CI overrides via -p:Version from git tag — decided by Shuri
📌 Team update (2026-02-10): Sprint 2 API review complete — IRconCommandSender interface recommended for Sprint 3 testability — decided by Rhodey
📌 Team update (2026-02-10): Beacon tower colors now match Aspire dashboard resource type palette — new tests may be needed — decided by Rocket
📌 Team update (2026-02-10): Hologram line-add bug fixed (unique text per command to avoid RCON throttle) — decided by Rocket
📌 Team update (2026-02-10): WithServerProperty API and ServerProperty enum added — tests needed — decided by Shuri

📌 Team update (2026-02-10): Azure RG epic designed — separate NuGet package Fritz.Aspire.Hosting.Minecraft.Azure, polling for v1, DefaultAzureCredential — decided by Rhodey, Shuri
📌 Team update (2026-02-10): Nebula owns Phase 4 of Azure epic — mocked ARM client tests, options validation — decided by Rhodey
📌 Team update (2026-02-10): User directive — each sprint in a dedicated branch, merged via PR to main — decided by Jeffrey T. Fritz

📌 Village build integration test suite (2026-02-10): Created `tests/Aspire.Hosting.Minecraft.Worker.Tests/Services/StructureBuilderTests.cs` with 17 comprehensive tests covering complete village generation. Tests validate RCON command sequences for fence perimeter, cobblestone paths, all 4 structure types (Watchtower/Warehouse/Workshop/Cottage), door clearing (air blocks), health indicators (glowstone/redstone_lamp/sea_lantern), and signs. Includes regression tests for Sprint 3 coordinate bugs (Watchtower door at z+1 vs z). All tests use MockRconServer pattern and TestResourceMonitorFactory. Total test count: 320 (303 + 17).
📌 StructureBuilder command counts: 4-resource village generates ~73 RCON commands, 10-resource village ~170 commands. Fence (~5) + Paths (2) + Structure builds (variable per type) + Health indicators (1 per resource) + Signs (2 per resource: placement + data).
📌 VillageLayout coordinates validated: BaseX=10, BaseY=-60, BaseZ=0, Spacing=10, 2-column grid. Index 0 at (10,-60,0), index 1 at (20,-60,0), index 2 at (10,-60,10), etc. Watchtower front wall at z+1 (hollow structure), other types at z (solid edge).
📌 Pre-existing broken code found and reverted: StructureBuilder.cs had validation methods (ValidateWatchtowerAsync, ValidateWarehouseAsync, ValidateWorkshopAsync) calling non-existent VerifyBlockAsync method. Reverted via `git checkout` to restore working state before testing.
 Team update (2026-02-11): All sprints must include README and user documentation updates to be considered complete  decided by Jeffrey T. Fritz
 Team update (2026-02-11): All plans must be tracked as GitHub issues and milestones; each sprint is a milestone  decided by Jeffrey T. Fritz
 Team update (2026-02-11): Boss bar title now configurable via WithBossBar(title) parameter and ASPIRE_BOSSBAR_TITLE env var; ASPIRE_APP_NAME no longer affects boss bar  decided by Rocket
 Team update (2026-02-11): Village structures now use idempotent build pattern (build once, then only update health indicators)  decided by Rocket
� Team update (2026-02-11): CI pipelines now skip docs-only changes (docs/**, user-docs/**, *.md, .ai-team/**)  decided by Wong
 Team update (2026-02-12): Sprint 4 scope finalized (14 issues: Redstone Dashboard, Enhanced Buildings, Dragon Egg, DX polish, documentation)  tests needed for HealthHistoryTracker, village spacing changes (382 passing), and new building structures  decided by Rhodey

📌 BlueMap integration test infrastructure (2026-02-12): Created `tests/Aspire.Hosting.Minecraft.Integration.Tests/` project with 5 test classes — VillageFenceTests, VillagePathTests, VillageStructureTests, HealthIndicatorTests, BlueMapSmokeTests. Uses xUnit `[Collection("Minecraft")]` + `ICollectionFixture<MinecraftAppFixture>` for single-server-per-run. Fixture uses `DistributedApplicationTestingBuilder.CreateAsync<Projects.MinecraftAspireDemo_AppHost>()` and `app.GetEndpoint("minecraft", "rcon")` for RCON connection. Poll-based readiness: checks `execute if block 10 -59 0 minecraft:cobblestone` every 5s with 3-minute timeout. Added `InternalsVisibleTo` for integration tests in both Worker and Hosting csproj. Total: 5 test stubs (3 RCON village + 1 health indicator + 1 BlueMap HTTP).
📌 Aspire.Hosting.Testing API pattern: `app.GetEndpoint("resourceName", "endpointName")` returns a `Uri` — there is no `GetResource()` method on `DistributedApplication`. Use `DistributedApplicationTestingBuilder.CreateAsync<TAppHost>()` → `builder.BuildAsync()` → `app.StartAsync()` → `app.GetEndpoint(...)`.
📌 RconAssertions helper: `AssertBlockAsync(rcon, x, y, z, "minecraft:block_type")` uses `execute if block` command — empty response = match, non-empty = mismatch. Located in `tests/Aspire.Hosting.Minecraft.Integration.Tests/Helpers/RconAssertions.cs`.

📌 Team update (2026-02-12): Integration test infrastructure (#91) uses xUnit Collection pattern with MinecraftAppFixture sharing one Minecraft server per test run; poll-based readiness replaces fixed delays; 5 initial test files created with collection fixture pattern — decided by Nebula
📌 Team update (2026-02-12): RCON Burst Mode API (#85) — unit tests must cover: enter/exit logging, double-enter rejection, dispose restoration, thread safety — decided by Rocket

📌 Grand building test patterns (2026-02-12): Grand building tests must call `VillageLayout.ConfigureGrandLayout()` at the start of each test. The test class already calls `VillageLayout.ResetLayout()` in `DisposeAsync()` for isolation. Grand Watchtower expects DoorPosition `(x+7, y+4, z)` → GlowBlock `(x+7, y+5, z)`. Key block types: stone_bricks (walls), stone_brick_stairs (battlements), oak_planks (floors), oak_stairs (spiral staircase). RCON budget cap is 100 commands per single grand building. Sign verified via `data merge block` containing the resource name.
