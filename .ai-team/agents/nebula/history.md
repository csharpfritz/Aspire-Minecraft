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
