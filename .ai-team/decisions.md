# Decisions

> Shared decision log. All agents read this before starting work. Scribe merges new decisions from the inbox.

### 2026-02-10: NuGet hardening completed — floating deps pinned, SourceLink and deterministic builds added

**By:** Shuri
**What:** Audited all three packages and found blockers (floating `Version="*"` deps, no SourceLink, no package validation, no CI/CD, 41 MB hosting package). Resolved in Sprint 1: pinned 6 deps to exact versions, added `GenerateDocumentationFile`, `EnablePackageValidation`, `Deterministic`, `ContinuousIntegrationBuild`, `EmbedUntrackedSources`, and `Microsoft.SourceLink.GitHub` to `Directory.Build.props`. Created per-package README.md files. Kept the 23 MB OpenTelemetry Java agent embedded — runtime download deferred to Sprint 2 due to container networking complexity.
**Why:** NuGet.org rejects floating versions. Pinned versions ensure reproducible builds. SourceLink enables debugger source mapping. Per-package READMEs improve nuget.org presentation. OTel jar stays embedded to avoid offline/restricted environment issues in v0.1.0.
**Status:** ✅ Resolved. Remaining: `PackageIcon` not yet added. OTel jar extraction is Sprint 2.


### 2026-02-10: Proposed feature ideas for Aspire-Minecraft

**By:** Rocket

**What:** A prioritized set of 18 new in-world interaction features organized by effort and impact across 3 tiers.

**Why:** The current worker is mostly passive (holograms, scoreboards, structures, chat). These features add drama, atmosphere, and delight — making health changes feel like real events in the game world.

**Must-Have (Size S, Sprint 1):** Boss Bar Health Meter, Title Screen Alerts, Sound Effects on Events, Weather = System Health, Particle Effects at Structures.

**Nice-to-Have (Size S–M, Sprint 2):** Action Bar Metrics Ticker, Fireworks on All-Green Recovery, Guardian Mobs per Resource, World Border Pulse, Beacon Towers per Resource Type, Deployment Fanfare.

**Stretch Goals (Size M–L, Sprint 3):** Resource Village with Themed Architecture, Redstone Heartbeat Circuit, Nether Portal Frames, Live Log Wall, Player /trigger Commands, Advancement Achievements, Resource Dependency Rail Network.

**Backlog (not in 3-sprint arc):** Nether Portal Frames, Live Log Wall, /trigger Commands.


### 2026-02-10: 3-Sprint Plan for Aspire-Minecraft

**By:** Rhodey
**What:** Three-sprint roadmap from "builds locally" to "conference-demo-ready NuGet packages with CI/CD and blog coverage."
**Why:** Working code but unpublishable packages. 18 features need sequencing so each sprint ends shippable and demo-able.

**Sprint 1 "Ship It" (~22 items):**
- Shuri: Pin floating deps, NuGet hardening, extract otel jar, verify pack output.
- Rocket: Boss bars, title alerts, sounds, weather, particles (all Size S).
- Nebula: Test project structure, RCON unit tests, health check tests, pack smoke test.
- Wong: build.yml CI workflow, release.yml stub, branch protection.
- Rhodey: PR review gate, public API contract, CONTRIBUTING.md.
- Mantis: Blog outline, demo screenshots.

**Sprint 2 "Polish & Atmosphere" (~20 items):**
- Shuri: Configuration builder pattern, XML docs, RCON batching audit.
- Rocket: Action bar ticker, fireworks, guardian mobs, beacons, deployment fanfare.
- Nebula: Integration test harness, Sprint 1 feature tests, config tests, coverage gate.
- Wong: NuGet publish on tag, test execution in CI, Dependabot, issue templates.
- Rhodey: API review, cut v0.1.0 release.
- Mantis: Publish v0.1.0 blog, social media, begin deep-dive draft.

**Sprint 3 "Showstopper" (~18 items):**
- Shuri: World border pulse, placement algorithm, RCON rate-limiting.
- Rocket: Themed village (L), redstone heartbeat, achievements, rail network.
- Nebula: Sprint 2 feature tests, world border tests, E2E demo test, perf test.
- Wong: Changelog gen, symbol packages, GitHub Pages docs, CodeQL.
- Rhodey: API freeze v0.2.0, automated release, demo script review.
- Mantis: Deep-dive blog, conference demo post, README overhaul.

**Cut line:** Rail Network drops first; Resource Village + Achievements are conference must-haves.


### 2026-02-10: CI/CD pipeline — build.yml + release.yml created

**By:** Wong
**What:** Created two GitHub Actions workflows: `build.yml` (CI on push/PR to main, ubuntu+windows matrix, restore→build→test→pack→upload) and `release.yml` (NuGet publish on `v*` tag, GitHub Release creation). Also added `.github/PULL_REQUEST_TEMPLATE.md`. No separate PR-validation workflow — `build.yml` covers PR triggers.
**Why:** Sprint 1 blocker — no CI/CD existed. Packages can't ship to nuget.org without an automated publish pipeline. The matrix build ensures cross-platform correctness. Tag-triggered release keeps publishing intentional. `NUGET_API_KEY` secret must be configured in repo settings before first release.


### 2026-02-10: Test project structure and InternalsVisibleTo pattern established

**By:** Nebula
**What:** Created tests/Aspire.Hosting.Minecraft.Rcon.Tests and tests/Aspire.Hosting.Minecraft.Tests with xUnit and Microsoft.NET.Test.Sdk. Added InternalsVisibleTo to both source projects. Changed MinecraftHealthCheck.ParseConnectionString from private to internal for testability. 62 tests (45 RCON + 17 hosting), 0 failures.
**Why:** CI/CD pipeline requires test projects to exist and pass. The InternalsVisibleTo pattern enables testing of internal types (RconPacket, endpoint constants, ParseConnectionString) without exposing them publicly.


### 2026-02-10: FluentAssertions removal and assertion library decision (consolidated)

**By:** Nebula, Jeffrey T. Fritz
**What:** FluentAssertions 8.8.0 (Xceed) had commercial licensing incompatible with this MIT-licensed project. Jeff directed the team to drop it entirely. Nebula replaced all 95 assertion calls across 5 test files with xUnit's built-in `Assert` class. 62 tests, 0 failures after migration. Zero new dependencies added.
**Why:** Nebula flagged the licensing concern; Jeff confirmed no FluentAssertions. xUnit `Assert` was chosen over Shouldly/TUnit because all existing patterns (equality, boolean, null, empty, contains, throws) mapped 1:1 to `Assert.*` — no new package needed.
**Status:** ✅ Resolved. FluentAssertions fully removed from both .csproj files and all test code.


### 2026-02-10: Track all work as GitHub issues with team member labels

**By:** Jeffrey T. Fritz (via Copilot)
**What:** All sprint plan items opened as GitHub issues. Labels created for each team member (rhodey, shuri, rocket, nebula, wong, mantis) and sprint (sprint-1, sprint-2, sprint-3). 34 issues created across 3 sprints. Labels should have distinct, visually meaningful colors for easy identification.
**Why:** User directive — ensures visibility and accountability for all planned work.


### 2026-02-10: Single NuGet package consolidation (consolidated)

**By:** Jeffrey T. Fritz, Shuri
**What:** Jeff directed that the RCON client, worker service, and Aspire hosting integration should ship as a single NuGet package. Shuri implemented the consolidation: only `Aspire.Hosting.Minecraft` is now packable. Rcon project set to `IsPackable=false` with its assembly embedded via `PrivateAssets="All"` + `BuildOutputInPackage`. Worker set to `IsPackable=false` (stays separate — it's a standalone process using `Microsoft.NET.Sdk.Worker`). Rcon's transitive dependency (`Microsoft.Extensions.Logging.Abstractions`) surfaced as a direct PackageReference in the Hosting project.
**Why:** Simplifies the consumer experience — one package to install. The Rcon library is a pure implementation detail. The Worker is referenced via the `WithAspireWorldDisplay<TWorkerProject>()` generic type parameter, not as a library dependency.
**Verified:** `dotnet restore` ✅, `dotnet build -c Release` ✅, `dotnet pack -c Release -o nupkgs` ✅ (single package: 39.6 MB), `dotnet test` ✅ (62 tests pass).
**Status:** ✅ Resolved.


### 2026-02-10: Redstone Dependency Graph — Design & Implementation (consolidated)

**By:** Jeffrey T. Fritz (idea), Rocket (implementation)
**Issue:** #36

**What:** Visualize Aspire resource dependencies as redstone wire circuits in the Minecraft world. Each resource has a structure, and redstone lines connect them to show the dependency graph. `RedstoneDependencyService` implements L-shaped routing (X then Z), repeaters every 15 blocks, redstone lamps at entrances, health-reactive circuit breaking/restoring. Originally a `BackgroundService`, later converted to `AddSingleton<>()` called from `MinecraftWorldWorker` (see: "Sprint 3 service lifecycle" decision).

**Key decisions:**
1. L-shaped routing avoids complex A* pathfinding.
2. Circuit breaking — remove redstone block + break wire every 5th position on unhealthy.
3. CommandPriority.Low for building to avoid starving higher-priority commands.
4. Wire positions at BaseY, Z-1 — paths run in front of structures.
5. Lever switches are visual-only (see: "Service Switches" decision) — they do not control Aspire resources.

**Technical considerations:**
- Redstone wires have a max range of 15 blocks — repeaters used for distant services
- Should respect dependency ordering — stopping a database should warn about dependent services
- Could use redstone signal strength to indicate health/load

**Status:** ✅ Implemented (lifecycle updated).

### 2026-02-10: NuGet PackageId renamed to Fritz.Aspire.Hosting.Minecraft

**By:** Shuri (requested by Jeffrey T. Fritz)
**What:** Renamed the NuGet PackageId from `Aspire.Hosting.Minecraft` to `Fritz.Aspire.Hosting.Minecraft` in the csproj. Updated all documentation (blog post, demo script, CONTRIBUTING.md) to reference the new package name. C# namespaces, project folders, assembly names, and solution structure are unchanged — only the NuGet package identity changed. User explicitly chose `Fritz` as the prefix (rejected `CommunityToolkit` alternative).
**Why:** The `Aspire.Hosting` prefix is reserved by Microsoft on NuGet.org. Publishing under that prefix would be rejected. The `Fritz` prefix avoids the reserved namespace while keeping the package discoverable. Consumers still `using Aspire.Hosting.Minecraft;` — the install command is now `dotnet add package Fritz.Aspire.Hosting.Minecraft`.
**Verified:** restore ✅, build ✅ (0 errors), pack ✅ (`Fritz.Aspire.Hosting.Minecraft.0.1.0.nupkg`), test ✅ (207 tests pass).
**Status:** ✅ Resolved.


### 2026-02-10: Blog outline structure and media plan for v0.1.0

**By:** Mantis
**What:** Created three deliverables in `docs/blog/`: `v0.1.0-release-outline.md` (full blog post outline with 7 sections, placeholder code snippets, social media copy), `v0.1.0-media-plan.md` (18 visual assets with capture instructions), and `v0.1.0-demo-script.md` (10-minute 4-act demo script).
**Why:** First public release — the blog post is the primary announcement channel. .NET devs using Aspire are the audience. Demo climax is the "break" moment (stopping a service and watching 6 feedback channels react). 18 media assets cover blog, social media, and conference slides. Media captures require Sprint 1 features from Rocket.
**Dependencies:** Rocket's Sprint 1 features (boss bar, weather, title alerts, sounds, particles) must be complete before media capture. Blog references actual sample `Program.cs`.


### 2026-02-10: Sprint 1 proactive test coverage for Rocket's features

**By:** Nebula
**What:** Created `tests/Aspire.Hosting.Minecraft.Worker.Tests` with 145 tests covering all 5 Sprint 1 features (particles, title alerts, weather, boss bar, sounds) plus state transitions, health→RCON mapping, and feature opt-in behavior. Solution total: 207 tests across 3 projects, all passing.
**Why:** Proactive testing — writing tests before implementation ensures expected RCON command syntax is documented, state transition edge cases are covered, and Rocket has concrete test expectations to code against.
**Key decisions:** No MockRconService (sealed class, no interface) — tests validate command format via static helper. Commented-out stubs for opt-in tests await Rocket's extension methods. Health ratio thresholds are opinionated (Weather: 100%=clear, 20-99%=rain, <20%=thunder; BossBar: ≥75%=green, 25-74%=yellow, <25%=red).
**Testability concern:** `RconService` is sealed with no interface — consider adding `IRconCommandSender` in Sprint 2.
**Status:** ✅ Complete.


### 2026-02-10: Sprint 1 feature decisions — opt-in architecture, state tracking, health thresholds

**By:** Rocket
**Issues:** #3, #5, #7, #8, #10
**What:** Each Sprint 1 feature (particles, title alerts, weather, boss bar, sounds) is enabled by a dedicated environment variable (`ASPIRE_FEATURE_{NAME}=true`) set via builder extension methods, with conditional service registration in the Worker. Services injected as nullable primary constructor parameters. Particles/titles/sounds fire per-resource; weather/boss bar reflect aggregate fleet health. State tracking (`_lastWeather`, `_lastValue`, `_lastColor`) avoids redundant RCON commands.
**Health thresholds:** Weather: 100%=clear, ≥50%=rain, <50%=thunder. Boss bar: 100%=green, ≥50%=yellow, <50%=red.
**Why:** Follows existing env var pattern. Opt-in ensures backward compatibility and zero additional RCON traffic for unused features. State tracking conserves server tick budget.
**Status:** ✅ Implemented.


### 2026-02-10: Public API surface contract established

**By:** Shuri
**Issue:** #12
**What:** Audited all public types and established intentional API surface. Made `MinecraftHealthCheck` internal (hosting). Made all Worker types internal (15 classes). Kept public: `MinecraftServerBuilderExtensions` (consumer entry point with 11 methods), `MinecraftServerResource`, and 5 RCON types (`RconClient`, `RconConnection`, `RconResponseParser`, `TpsResult`, `MsptResult`, `PlayerListResult`, `WorldListResult`).
**Why:** Worker is a standalone service (`IsPackable=false`) — all its types are implementation details. RCON types kept public for consumers who want custom RCON commands. `EnablePackageValidation` catches accidental API surface changes.
**Status:** ✅ Resolved.


### 2026-02-10: Sprint 2 API review — consistent, no breaking changes

**By:** Rhodey
**Scope:** Public API surface review of `src/Aspire.Hosting.Minecraft/` after Sprint 2 completion

**What:** All 10 feature extension methods (5 Sprint 1 + 5 Sprint 2) follow identical patterns: same signature shape, guard clause, env var naming (`ASPIRE_FEATURE_*`), fluent return, XML docs. No breaking changes needed.

**Key findings:**
- Environment variable naming consistent: `ASPIRE_FEATURE_{NAME}` for features, `ASPIRE_RESOURCE_{NAME}_TYPE/URL/HOST/PORT` for metadata, `ASPIRE_APP_NAME` for app-level config.
- XML documentation complete on all public types and methods.
- Access modifiers correct: public API intentional, internal types properly hidden.

**Recommendations for Sprint 3:**
1. Add `WithAllFeatures()` convenience method
2. Extract duplicated `ParseConnectionString` to shared utility
3. Add `IRconCommandSender` interface for testability
4. Tighten feature env var checks to `== "true"` instead of `!string.IsNullOrEmpty`
5. Consider `WithAllMonitoredResources()` auto-discovery
6. API freeze before v0.2.0

**Status:** ✅ Approved for v0.1.0 release cut.


### 2026-02-10: Beacon tower glass colors match Aspire dashboard resource type palette

**By:** Rocket (requested by Jeffrey T. Fritz)
**What:** Changed `BeaconTowerService` from simple green/red glass to resource-type-specific colors matching the Aspire dashboard icon palette. Project=blue, Container=purple, Executable=cyan, unknown type=light blue, unhealthy=red, starting=yellow. `GetGlassBlock()` method is `internal static` for testability. Implements user directive that beacon beams should match Aspire dashboard resource type colors.
**Why:** Green/red scheme gave no visual distinction between resource types. Dashboard uses blue for projects, purple for containers, teal for executables — beacon beams reinforce the same color language in-world. Health state overrides type color for at-a-glance alerting.
**Status:** ✅ Resolved. Build passes, 248 tests pass.


### 2026-02-10: Hologram line-add commands must use unique text to avoid RCON throttle

**By:** Rocket
**What:** Fixed `HologramManager` using identical placeholder text (`&7...`) for all `dh line add` commands. The `RconService` 250ms throttle silently dropped duplicate commands in rapid succession, causing fewer hologram lines than expected. Changed to `&7line{n}` for unique commands.
**Why:** The RCON throttle is intentional for preventing server flood. The fix works with the throttle rather than disabling it. Any future service issuing identical RCON commands in a tight loop must use unique command strings.
**Status:** ✅ Resolved. Build passes, 248 tests pass.


### 2026-02-10: Sprint 2 feature decisions — action bar ticker, beacon towers, boss bar app name

**By:** Rocket
**Issues:** #38, #20, #22
**What:** Three new Sprint 2 features following the established opt-in env var pattern. Boss bar now supports configurable app name via `ASPIRE_APP_NAME` (implements user directive: boss bar text should show system name from Aspire, not generic text). Action bar ticker cycles TPS/MSPT/healthy count/RCON latency. Beacon towers build iron+beacon+glass structures per resource.

**Key decisions:**
- `WithBossBar()` added optional `string? appName = null` for backward compatibility.
- Action bar ticker reads fresh metrics each tick (not cached from main loop).
- Beacon towers at Z=8 offset to avoid collision with existing structures at Z=0.
- Single-layer iron base (minimum for beacon activation).
- Plain strings for action bar, consistent with Sprint 1.

**Status:** ✅ Implemented. 248 tests pass.


### 2026-02-10: NuGet package version defaults to `0.1.0-dev`, overridden by CI

**By:** Shuri
**What:** Changed `<Version>` in csproj from `0.1.0` to `0.1.0-dev`. Local builds produce pre-release packages. The release workflow passes `-p:Version=X.Y.Z` (from git tag) which overrides the csproj default.
**Why:** Previously every NuGet publish produced version `0.1.0` regardless of the git tag. The `-dev` suffix distinguishes local from release builds. MSBuild CLI properties always win over csproj values.
**Status:** ✅ Resolved. Wong's release workflow update is the companion change.


### 2026-02-10: Server Properties API — WithServerProperty + Enums + File Loading (consolidated)

**By:** Shuri

**What:** Added comprehensive server.properties configuration API:
1. `WithServerProperty(string, string)` and `WithServerProperties(Dictionary<string, string>)` extension methods, plus 6 convenience methods (`WithGameMode`, `WithDifficulty`, `WithMaxPlayers`, `WithMotd`, `WithWorldSeed`, `WithPvp`). All set env vars following the itzg/minecraft-server convention (property name → UPPER_SNAKE_CASE).
2. `ServerProperty` enum (24 members), `MinecraftGameMode` enum (4 members), `MinecraftDifficulty` enum (4 members), corresponding typed overloads.
3. `WithServerPropertiesFile()` for bulk property loading from disk.

**Why:** Users previously had to look up `server.properties` key names and pass raw strings. The enum gives IntelliSense discovery. Typed enums prevent typos. File-based loading lets users maintain a standard `server.properties` file.
**Design choices:** PascalCase→UPPER_SNAKE_CASE conversion. `WithServerPropertiesFile` reads at build/configuration time. Last-write-wins semantics.
**Status:** ✅ Resolved.

### 2026-02-10: Sprint 2 — XML documentation, RCON throttle, config builder pattern review

**By:** Shuri
**Issues:** #16, #21
**What:**
1. Added comprehensive XML documentation to all public types and methods in both projects.
2. Added configurable RCON command throttle to Worker's `RconService` (default: disabled, production: 250ms). Per-command-string deduplication prevents server flood during rapid health oscillations.
3. Reviewed configuration builder pattern — existing `With*()` fluent extension methods already serve as the config builder. No formal options-class builder needed. Recommend closing Issue #21.

**Status:** ✅ Complete.


### 2026-02-10: Release workflow now extracts version from git tag

**By:** Wong
**What:** Updated `.github/workflows/release.yml` to extract the semantic version from the git tag (`v0.2.1` → `0.2.1`) and pass it to `dotnet build` and `dotnet pack` via `-p:Version=`. GitHub Release name now includes the version. CI workflow (`build.yml`) intentionally unchanged.
**Why:** Previously every tag-triggered release produced `0.1.0` packages regardless of the actual tag. The tag is now the single source of truth for release versions.
**Status:** ✅ Resolved.


### 2026-02-10: User directive — sprint branches with PRs
**By:** Jeffrey T. Fritz (via Copilot)
**What:** Each sprint's work should be done in a dedicated branch named after that sprint, then pushed and merged via PR to main on GitHub.
**Why:** User request — captured for team memory


### 2026-02-10: E2E cascade failure scenario + 25-resource performance tests

**By:** Nebula
**Issue:** #31

**What:** Added comprehensive test coverage for Sprint 2 features and beyond:

1. **Sprint 2 feature integration tests** — GuardianMobService (8 tests), BeaconTowerService (16 tests including `GetGlassBlock` unit tests), FireworksService (7 tests), DeploymentFanfareService (7 tests), ActionBarTickerService (5 tests). All follow the established MockRconServer integration test pattern.

2. **E2E cascade failure scenario** (`Scenarios/CascadeFailureScenarioTests.cs`) — 4 tests exercising multi-service interaction: 5 resources healthy → 1 fails → 2 more cascade → boss bar drops to red → guardians switch to zombies → all recover → fireworks launch → golems return.

3. **25-resource performance tests** (`Performance/LargeResourceSetTests.cs`) — 10 tests proving StructureBuilder, BeaconTowerService, HologramManager, BossBarService, GuardianMobService, and ParticleEffectService all handle 25 resources without exceptions.

**Verified:** 303 tests across 3 projects, 0 failures.
**Status:** ✅ Complete.


### 2026-02-10: API Surface Freeze for v0.2.0

**By:** Rhodey
**Issue:** #24

**What:** Froze the public API surface for the v0.2.0 release. Created `docs/api-surface.md` documenting all 31 public extension methods on `MinecraftServerBuilderExtensions`, 4 public types in `Aspire.Hosting.Minecraft`, and 6 public RCON types.

**Key findings:**
- All 13 feature methods (Sprint 1–3) follow identical patterns: guard clause → env var → fluent return. No deviations.
- No internal types leak through the public API.
- XML documentation is complete on every public type and method.
- `WithWorldBorderPulse` was incorrectly grouped under Sprint 2 in the demo — moved to Sprint 3.

**Status:** ✅ Resolved. Any API additions beyond this point require explicit review before release.


### 2026-02-10: Azure Resource Group Integration — Epic Design & SDK Research (consolidated)

**By:** Rhodey (epic design), Shuri (SDK research)
**Date:** 2026-02-10
**Scope:** New epic — Azure Resource Group → Minecraft integration
**Document:** `docs/epics/azure-resource-group-integration.md`, `docs/epics/azure-sdk-research.md`

**Decisions Made:**
1. Separate NuGet package: `Fritz.Aspire.Hosting.Minecraft.Azure` — isolates Azure SDK dependencies (~5 MB most users don't need).
2. Azure monitor is a new resource discovery source, not a new rendering pipeline.
3. Polling for v1, Event Grid deferred. 30-second default interval.
4. Aspire-only for v1. Standalone mode is Phase 5.
5. `MaxResources = 50` default with auto-exclude of infrastructure noise.
6. `DefaultAzureCredential` as the default auth.
7. For v1: layered health (provisioning state + Resource Health API).

**Open Questions for Jeff:**
- Package naming: `Fritz.Aspire.Hosting.Minecraft.Azure` vs `Fritz.Azure.Minecraft`?
- Should mixed mode (Aspire + Azure resources in same world) be supported in v1?
- Default exclude list for infrastructure resource types?

**Team Impact:**
- Shuri: Owns Phases 1 and 3 (ARM client, auth, options, NuGet package scaffold)
- Rocket: Owns Phase 2 (Azure type → Minecraft structure mapping)
- Nebula: Owns Phase 4 (mocked ARM client tests, options validation)

### 2026-02-10: Advancement Achievements use RCON titles instead of datapacks

**By:** Rocket
**Issue:** #32
**What:** `AdvancementService` grants four infrastructure achievements using RCON `title @a title/subtitle` with JSON text components and `playsound`. No Minecraft datapack advancements are used.
**Why:** Mounting custom advancement JSON datapacks into the Minecraft container is complex and fragile. Title + subtitle + sound gives equivalent player feedback without container filesystem changes. Achievements tracked per-session via `HashSet<string>`.
**Status:** ✅ Implemented. Follows opt-in pattern (`ASPIRE_FEATURE_ACHIEVEMENTS`, `WithAchievements()`).


### 2026-02-10: Azure Resource Visualization Design

**By:** Rocket
**Date:** 2026-02-10
**Document:** `docs/epics/azure-minecraft-visuals.md`

**What:** Designed the complete visual language for rendering Azure resources in Minecraft. Covers 15 Azure resource types mapped to unique Minecraft structures.

**Key Decisions:**
1. Azure district visually distinct from Aspire village (prismarine/quartz/end stone palette).
2. 3-column tiered layout grouping resources by functional tier.
3. District starts at X=60 with prismarine boulevard connecting to Aspire village.
4. Azure beacon colors: Compute=cyan, Data=blue, Networking=purple, Security=black, Messaging=orange, Observability=magenta.
5. Azure health states: Stopped=cobwebs, Deallocated=soul sand ring, Failed=netherrack fire on roof.

**Status:** 📐 Design complete — no implementation yet.


### 2026-02-10: Heartbeat service timing

**By:** Rocket
**Issue:** #27

**What:** `HeartbeatService` uses a 1–4 second pulse interval depending on health. Originally implemented as `BackgroundService` (via `AddHostedService`), later converted to `AddSingleton<>()` called from `MinecraftWorldWorker` to fix a startup race condition (see: "Sprint 3 service lifecycle" decision).

**Why:** Main worker loop runs on 10-second intervals — too slow for a heartbeat. RCON throttle deduplication handled by micro-varying volume (0.001 increments per tick).

**Status:** ✅ Implemented (lifecycle updated).


### 2026-02-10: Resource Village Layout & Themed Structures

**By:** Rocket
**Issue:** #25

**What:** Themed mini-buildings per Aspire resource type in a 2×N grid with 10-block spacing. Project=Watchtower, Container=Warehouse, Executable=Workshop, Unknown=Cottage. `VillageLayout` static class centralizes position calculations. Health indicator via redstone lamp in front wall.


### 2026-02-10: Service Switches — visual-only levers representing resource status (consolidated)

**By:** Rocket
**Issue:** #35

**What:** `ServiceSwitchService` with `WithServiceSwitches()` and `ASPIRE_FEATURE_SWITCHES` env var. Levers and lamps on each resource structure. Healthy=lever ON + glowstone, Unhealthy=lever OFF + unlit redstone lamp. Originally a `BackgroundService`, later converted to `AddSingleton<>()` called from `MinecraftWorldWorker` (see: "Sprint 3 service lifecycle" decision).

**Key decision:** Visual only — levers reflect state, they do not control Aspire resources. Manually flipping a lever in-game will be overwritten on next update cycle. This is by design for safety (prevents accidental resource control from game interface) and consistency with other display-only features (health lamps, holograms, boss bar, redstone dependency graph).

**Status:** ✅ Implemented (lifecycle updated).

### 2026-02-10: Village fence perimeter and pathway coordinate conventions

**By:** Rocket
**What:** Added `GetVillageBounds()` and `GetFencePerimeter()` to `VillageLayout`. Fence at ground level (`BaseY`), 4-block gap from buildings. Boulevard at `BaseX + StructureSize` (X=17). Future services placing things around the village edge should use these methods.
**Status:** ✅ Implemented (updated: fence moved to ground level, gap increased to 4 blocks).


### 2026-02-10: Resource Dependency Placement + RCON Rate-Limiting

**By:** Shuri
**Issue:** #29

**What:**
1. **RCON rate-limiting:** `CommandPriority` enum, token bucket rate limiter (default 10 commands/sec). High-priority commands bypass limits; low-priority commands queue in bounded channel (100, DropOldest).
2. **Dependency placement:** `ResourceInfo` carries `Dependencies` list from `ASPIRE_RESOURCE_{NAME}_DEPENDS_ON` env vars. `VillageLayout.ReorderByDependency()` uses BFS topological sort.
3. **Hosting integration:** `WithMonitoredResource()` accepts `params string[] dependsOn` and auto-detects `IResourceWithParent`.

**Status:** ✅ Resolved. Build passes, 303 tests pass.


### 2026-02-10: Ephemeral Minecraft world by default, WithPersistentWorld() opt-in

**By:** Shuri (requested by Jeffrey T. Fritz)
**What:** Removed the default named Docker volume from `AddMinecraftServer()`. World data is now ephemeral. Added `WithPersistentWorld()` for opt-in persistence.
**Why:** Persistent worlds cause confusion during development — old structures remain from previous sessions.
**Status:** ✅ Resolved.


### 2026-02-10: World Border Pulse on Critical Failure

**By:** Shuri
**Issue:** #28
**What:** `WorldBorderService` and `WithWorldBorderPulse()`. World border shrinks from 200→100 blocks over 10s when >50% of resources are unhealthy, restores to 200 over 5s on recovery. Red warning tint at 5 blocks from border edge.
**Why:** Dramatic visual/physical feedback for critical failures. Follows opt-in pattern (`ASPIRE_FEATURE_WORLDBORDER`).
**Status:** ✅ Implemented.


### 2026-02-10: Changelog, Symbol Packages, CodeQL Scanning

**By:** Wong
**Issue:** #26

**What:**
1. Changelog generation uses GitHub's built-in `generate_release_notes: true`.
2. NuGet symbol packages enabled via `IncludeSymbols`/`SymbolPackageFormat`. Release workflow pushes `.snupkg` explicitly.
3. CodeQL security scanning added as `.github/workflows/codeql.yml` — C# only, default query suite, weekly + push/PR triggers.
4. GitHub Pages/docfx deferred to a future sprint.


### Sprint 3 service lifecycle: no independent BackgroundServices for RCON-dependent features

**By:** Rocket
**What:** Converted HeartbeatService, RedstoneDependencyService, and ServiceSwitchService from `AddHostedService<>()` (independent BackgroundServices) to `AddSingleton<>()` called by MinecraftWorldWorker. Also fixed beacon tower positions to derive from VillageLayout instead of hardcoded offsets.
**Why:** Independent BackgroundServices start before RCON is connected and before resources are discovered, causing all Sprint 3 features to silently fail. The established pattern (used by WorldBorderService, AdvancementService, BeaconTowerService, etc.) is singleton + nullable constructor injection + calls from the main worker loop. Beacon positions used a hardcoded BaseZ=14 that overlapped with row-1 structures (z=10–16), blocking beacon sky access for 2 of 4 resources.
**Rule:** Any feature service that uses RCON or depends on discovered resources MUST be registered as `AddSingleton<>()` and called from MinecraftWorldWorker — never as an independent `AddHostedService<>()`.
**Status:** ✅ Resolved. All 303 tests pass. Build clean (0 errors, 0 warnings).

### 2026-02-11: Minecraft building rules and constraints

**By:** Jeffrey T. Fritz (via Copilot)

**What:** When building structures and infrastructure in the Minecraft world, follow these mandatory constraints:

1. **Fences and barriers must sit ON the ground surface** — place at `BaseY` (y=-60 for superflat worlds), NOT `BaseY + 1`. Fences at y=-59 float in the air.

2. **Fences must be at least 4 blocks away from any building perimeter** — this provides adequate clearance and visual separation. The previous 1-2 block gap was too tight.

3. **Building footprint accounting** — when calculating fence perimeters around villages/groups, the village bounds already include the full structure footprint (7×7 for current structures). Offsets should be applied FROM those bounds, not from structure origins.

4. **Beacon placement must avoid structure overlap** — beacons require clear sky access. Position them dynamically based on structure size and layout (e.g., `z + StructureSize + 1`) rather than using hardcoded offsets that may conflict with multi-row grids.

5. **Ground level assumption** — for superflat worlds, `BaseY = -60` is the grass surface. Structures place floors at BaseY, walls at BaseY+1 and up, fences/paths at BaseY.

**Why:** These constraints ensure in-world structures render correctly in Minecraft:
- Floating fences look broken
- Structures too close to fences feel cramped
- Beacons without sky access don't show beams
- Y-level consistency prevents visual glitches

These rules were established after fixing Sprint 3 bugs where fences floated and beacons were blocked by structure overlap.


### 2026-02-11: Use GitHub issues and milestones for planning
**By:** Jeffrey T. Fritz (via Copilot)
**What:** Going forward, record all plans as issues and milestones in GitHub. Each sprint is a milestone.
**Why:** User directive — centralizes planning in GitHub for visibility and tracking. Replaces ad-hoc SQL/plan.md tracking.


### 2026-02-11: Sprint completion definition includes documentation
**By:** Jeffrey T. Fritz (via Copilot)
**What:** Going forward, all sprints must include updates to README and user documentation to be considered complete.
**Why:** User directive — documentation is a first-class deliverable, not an afterthought. Ensures features are always properly documented when shipped.


### 2026-02-11: Boss Bar Title Configuration

**Date:** 2026-02-11  
**Decider:** Rocket  
**Status:** Implemented


**Context:**

The boss bar previously displayed "Aspire Fleet Health: 100 percent" which looked unpolished. Additionally, users wanted the ability to customize the boss bar title text.


**Decision:**

1. **Changed percentage formatting** from "100 percent" to "100%" for cleaner display
2. **Added optional `title` parameter** to `WithBossBar()` extension method
3. **Used dedicated environment variable** `ASPIRE_BOSSBAR_TITLE` instead of repurposing `ASPIRE_APP_NAME`
4. **Default title** is "Aspire Fleet Health" when not specified


**Implementation:**

- `WithBossBar(string? title = null)` sets `ASPIRE_BOSSBAR_TITLE` env var if title provided
- `BossBarService` reads env var at construction with fallback to default
- Boss bar displays as: `"{title}: {percentage}%"`


**Rationale:**

- Dedicated env var is clearer than overloading `ASPIRE_APP_NAME`
- Optional parameter follows existing Fluent API pattern
- Default value maintains backward compatibility
- Percentage symbol is more concise and professional than "percent" word


**Impact:**

- Breaking change: `ASPIRE_APP_NAME` no longer affects boss bar (only title parameter does)
- API surface updated to show optional title parameter
- No change required for users who don't pass a title (default behavior preserved)


### 2026-02-10: Peaceful Mode Implementation

**Date:** 2026-02-10  
**Decider:** Rocket (Integration Dev)  
**Status:** Implemented


**Context:**

User requested a feature to eliminate hostile mobs (zombies, skeletons, creepers) from the Minecraft world to create a safer environment for monitoring infrastructure.


**Decision:**

Implemented `WithPeacefulMode()` extension method using `/difficulty peaceful` Minecraft command instead of gamerules.


**Rationale:**

1. **`/difficulty peaceful` vs gamerule approach:**
   - `difficulty peaceful` is the standard Minecraft way to eliminate hostiles
   - Immediately removes all existing hostile mobs
   - Prevents hostile mob spawning
   - Preserves passive mob spawning (cows, pigs, sheep)
   - More idiomatic than using `doMobSpawning` gamerule (which stops ALL mobs)

2. **One-time execution pattern:**
   - Command executes once at server startup after RCON connection
   - No service class needed — single RCON command is sufficient
   - Follows initialization pattern similar to `WorldBorderService.InitializeAsync()`

3. **Env var: `ASPIRE_FEATURE_PEACEFUL`**
   - Consistent with other opt-in features
   - Checked directly in `MinecraftWorldWorker.ExecuteAsync()` after resource discovery
   - No conditional DI registration needed (no service class)


**Implementation:**

- Extension method: `MinecraftServerBuilderExtensions.WithPeacefulMode()`
- Worker logic: Direct check in `MinecraftWorldWorker.ExecuteAsync()`
- Demo updated: Added `.WithPeacefulMode()` to Sprint 3 features
- API surface doc updated


**Alternatives Considered:**

- **Gamerule `doMobSpawning false`:** Stops ALL mob spawning including passives
- **Separate service class:** Overkill for single one-time command
- **Server property `DIFFICULTY=peaceful`:** Container-level, but less flexible for opt-in pattern


**Impact:**

- Opt-in feature, no effect on existing deployments
- All existing tests pass
- Consistent with team's feature opt-in architecture


### 2026-02-11: Village Structure Idempotent Building Pattern

**Date:** 2026-02-11  
**Decider:** Rocket (Integration Dev)  
**Status:** Implemented


**Context:**

Village structures were being rebuilt every 10-second display update cycle, causing visible glitching in-game. The `StructureBuilder.UpdateStructuresAsync()` method was calling `BuildResourceStructureAsync()` for every resource on every cycle without checking if the structure already existed.

Additionally, cobblestone paths were placed at `BaseY - 1` (Y=-61) which is underground in superflat worlds, making them invisible to players.


**Decision:**

Implemented idempotent building pattern for village structures:

1. **Structure Tracking**: Added `HashSet<string> _builtStructures` to track which resources have had their structures built
2. **Build Once Pattern**: Modified update loop to:
   - Check if structure already exists via `_builtStructures.Contains(name)`
   - If not built: call `BuildResourceStructureAsync()` and add to set
   - If already built: only update health indicator via `PlaceHealthIndicatorAsync()`
3. **Path Y-Level Fix**: Reverted all cobblestone path placements from `BaseY - 1` to `BaseY` so they sit on grass surface


**Rationale:**

- **Performance**: Eliminates redundant RCON commands for structure building every 10 seconds
- **Visual Stability**: Prevents the "glitching" effect where structures briefly change shape during rebuilds
- **Element Preservation**: Prevents structure rebuilds from overwriting decorative elements (switches, signs, lamps)
- **Path Visibility**: Paths at `BaseY` replace grass blocks and are visible/walkable; paths at `BaseY - 1` are buried in dirt

This follows the same pattern already established for fence (`_fenceBuilt` flag) and paths (`_pathsBuilt` flag).


**Consequences:**

- *Positive:*
- Buildings remain stable and don't glitch every 10 seconds
- Paths are visible and walkable on the ground surface
- Switches, signs, and other decorative elements persist correctly
- Reduced RCON command volume (better performance)
- All 303 existing tests pass

- *Negative:*
- Structures cannot be "refreshed" if manually destroyed in-game without restarting the worker
- Resource name is used as tracking key (if a resource is renamed and added again, it would build a new structure)

- *Neutral:*
- Pattern is consistent with existing fence/path building flags
- Health indicators still update dynamically every cycle as intended


**Implementation Details:**

**File**: `src/Aspire.Hosting.Minecraft.Worker/Services/StructureBuilder.cs`

- Added field: `private readonly HashSet<string> _builtStructures = new(StringComparer.OrdinalIgnoreCase);`
- Modified `UpdateStructuresAsync()` to check `_builtStructures` before building
- Reverted path Y-coordinates from `VillageLayout.BaseY - 1` to `VillageLayout.BaseY` in three locations (main boulevard, cross paths, entry path)


**Alternatives Considered:**

1. **Time-based rebuild**: Only rebuild structures every N minutes instead of every cycle
   - Rejected: Still causes glitching, just less frequently
2. **Change detection**: Compare current vs desired structure state and only update differences
   - Rejected: Too complex; requires querying and parsing Minecraft world state via RCON
3. **Manual refresh command**: Add an RCON command to force structure rebuild
   - Deferred: Could be added later if needed, but not required for normal operation


**Related Decisions:**

- Fence perimeter uses `_fenceBuilt` flag (similar pattern)
- Paths use `_pathsBuilt` flag (similar pattern)
- Service switches already placed once then only update state on transitions


### 2026-02-10: Structure Build Validation with Graceful Degradation

**By:** Shuri  
**What:** Added post-build validation to StructureBuilder that verifies door and window blocks were placed successfully after each structure builds.  
**Why:** RCON commands can fail silently or be rate-limited, leaving incomplete structures. Validation helps detect these failures and logs warnings for observability. Uses graceful degradation (log warnings, don't throw exceptions) to avoid blocking the entire village build process if individual blocks fail validation.

**Implementation:**
- `VerifyBlockAsync()` helper uses `testforblock` RCON command to check block type at coordinates
- Each structure type has a corresponding `Validate*Async()` method called after building
- Validates door air blocks and window blocks (glass_pane, stained_glass variants) at expected coordinates
- Returns false on any exception to handle RCON failures gracefully


### 2026-02-11: User Documentation Structure in user-docs/

**By:** Vision

**What:** Created comprehensive user documentation in `user-docs/` folder with guides for getting started, configuration, features, troubleshooting, and examples. Documentation follows consistent structure across all feature guides and emphasizes user perspective over technical implementation.

**Why:** 

1. **Separation of concerns:** User documentation (`user-docs/`) is separate from technical documentation (`docs/`). Users need "how to use" guides, not architecture deep-dives.

2. **Consistent structure:** Each feature document follows the same pattern (What It Does → How to Enable → What You'll See → Use Cases → Code Example) making docs scannable and predictable.

3. **User-centric language:** Documentation uses concrete, observable descriptions ("glowing yellow lamp", "fast high-pitched heartbeat") instead of technical implementation details ("glowstone block at Y+5", "1000ms interval, pitch 24").

4. **Comprehensive examples:** Every feature includes working code examples that users can copy-paste. Examples section shows real-world patterns for common scenarios (full stack apps, demos, ambient monitoring, etc.).

5. **Troubleshooting first:** Dedicated troubleshooting guide covers common issues with specific solutions, not generic advice. Organized by category (installation, startup, world generation, features, connection, performance).

6. **Progressive disclosure:** Documentation starts simple (README → Getting Started → Configuration) then provides deep-dives for specific features. Users can go as deep as they need.

**Impact:** Users can now understand and use all features without reading source code or technical architecture documents. Documentation is ready for external users (NuGet package consumers).


### 2026-02-10: Documentation path filters added to GitHub Actions workflows

**By:** Wong

**What:** Added `paths-ignore` filters to build.yml, release.yml, and codeql.yml to skip CI/CD pipelines when only documentation files change. Ignored paths: `docs/**`, `user-docs/**`, `*.md` (root-level), `.ai-team/**`.

**Why:** Documentation updates (README, user docs, team state) don't affect code correctness, test outcomes, or package output. Running full build/test/pack cycles wastes CI runner minutes and creates noise in the workflow history. This change ensures CI resources are spent only on actual code changes.

**Impact:** PRs and commits that only touch markdown files or documentation directories will not trigger builds, tests, or CodeQL scans. The scheduled CodeQL run (Mondays) is unaffected and always runs regardless of path filters.


### 2026-02-11: Dynamic terrain detection replaces hardcoded superflat Y=-60

**By:** Rocket
**What:** Added `TerrainProbeService` that uses RCON binary search (`setblock ... keep`) to detect surface height at startup. All village services now use `VillageLayout.SurfaceY` instead of hardcoded `BaseY`. Path building made terrain-agnostic (clears all blocks, not just grass_block). Falls back to BaseY=-60 if detection fails, preserving backward compatibility.
**Why:** The village was hardcoded to Y=-60 (superflat grass layer). This broke on any other world type (normal, amplified, custom). Dynamic detection makes the village work on ANY world type while keeping superflat as the safe fallback. Binary search keeps RCON usage minimal (~8 commands) and the probe is non-destructive (cleans up placed blocks immediately).
